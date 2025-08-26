﻿using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.Emails;
using MailKit.Security;
using MimeKit;
using MailKit.Net.Smtp;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Layout.Properties;
using ClassNotes.API.Database;
using Microsoft.EntityFrameworkCore;
using ClassNotes.API.Database.Entities;
using iText.Kernel.Pdf.Canvas.Draw;
using ClassNotes.API.Constants;
using MimeKit.Text;
using CloudinaryDotNet;
using ClassNotes.API.Services.Audit;
using ClassNotes.Models;
using System.Threading.Channels;
using iText.Layout.Borders;

namespace ClassNotes.API.Services.Emails
{
    /// <summary>
    /// Este endpoint fue trabajado por AM y a su vez por HR
    /// En el caso se realizo una inveztigacion y el problema de saturacion de correos para lo cual se opto por distribuir cargas de correos entre 2 cuentas
    /// Sin embargo se utilizo hilos y semaforos para concurrencia, este codigo fue trabajado con una mezcla de esfuerzo y tambien Inteligencia artificial
    /// Se tuvo muchos problemas y unos que nisiquiera el propio Microsoft daba soluciones
    /// - Esta configurado para cargar las cuentas smpt disponibles en varios hilos concurrentes
    /// - Estas por cada cuenta SMTP son 7 hilos (de manera empirica probando me di cuenta que es el maximo que soporta el servidor)
    /// - Esta sujeta a cambios 
    /// </summary>
    public class EmailsService : IEmailsService
    {
        private readonly ClassNotesContext _context;
        private readonly Channel<EmailStudentListRequest> _messageQueue;
        private readonly IAuditService _auditService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailsService> _logger;

        /// <summary>
        /// Lista de cuentas SMTP configuradas con sus respectivos semáforos para control de concurrencia.
        /// Cada cuenta tiene su propio límite máximo de hilos concurrentes.
        /// </summary>
        private readonly List<SmtpAccountWrapper> _smtpAccounts;

        /// <summary>
        /// Clase interna que guarda una cuenta SMTP y su semáforo de control 
		/// </summary>
        private class SmtpAccountWrapper
        {

            // Configuración de la cuenta SMTP (host, puerto, credenciales)
            // Esta estan definidas en el Template la Nueva estructura 
            public SMTPAcountDto Account { get; }

            public SemaphoreSlim Semaphore { get; }


            /// <summary>
            /// Constructor que inicializa una nueva instancia del wrapper de cuenta SMTP
            /// </summary>
            /// <param name="account">Configuración de la cuenta SMTP</param>
            /// <param name="maxConcurrency"> Número máximo de operaciones concurrentes permitidas para esta cuenta.</param>
			/// 
            public SmtpAccountWrapper(SMTPAcountDto account, int maxConcurrency)
            {
                Account = account;
                Semaphore = new SemaphoreSlim(maxConcurrency);
            }
        }

        public EmailsService(ClassNotesContext context, Channel<EmailStudentListRequest> messageQueue, IAuditService auditService, IConfiguration configuration, ILogger<EmailsService> logger)
        {
            _context = context;
            _messageQueue = messageQueue;
            _auditService = auditService;
            _configuration = configuration;
            _logger = logger;
            var smtpAccounts = configuration.GetSection("SmtpAccounts").Get<List<SMTPAcountDto>>()
                ?? new List<SMTPAcountDto>();

            if (smtpAccounts.Count == 0)
                throw new InvalidOperationException("No hay cuentas SMTP configuradas.");

            // 7 hilos máximos por cuenta SMTP
            _smtpAccounts = smtpAccounts
                .Select(account => new SmtpAccountWrapper(account, 7))
                .ToList();
        }
        public async Task<ResponseDto<EmailDto>> SendEmailAsync(EmailDto dto)
        {
            var acquiredWrapper = await AcquireAccountAsync();

            try
            {
                return await SendWithAccount(acquiredWrapper, dto);
            }
            finally
            {
                acquiredWrapper.Semaphore.Release();
            }
        }

        private async Task<SmtpAccountWrapper> AcquireAccountAsync()
        {
            // Primero intentar adquirir inmediatamente
            foreach (var wrapper in _smtpAccounts)
            {
                if (await wrapper.Semaphore.WaitAsync(TimeSpan.Zero))
                {
                    return wrapper;
                }
            }

            // Si todas están ocupadas, esperar a cualquiera
            var waitTasks = _smtpAccounts.Select(w => w.Semaphore.WaitAsync()).ToArray();
            var completedTask = await Task.WhenAny(waitTasks);
            return _smtpAccounts[Array.IndexOf(waitTasks, completedTask)];
        }

        private async Task<ResponseDto<EmailDto>> SendWithAccount(SmtpAccountWrapper wrapper, EmailDto dto)
        {
            try
            {
                var email = new MimeMessage
                {
                    Subject = dto.Subject,
                    Body = new TextPart(TextFormat.Html) { Text = dto.Content }
                };
                email.From.Add(MailboxAddress.Parse(wrapper.Account.Username));
                email.To.Add(MailboxAddress.Parse(dto.To));

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(wrapper.Account.Host, wrapper.Account.Port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(wrapper.Account.Username, wrapper.Account.Password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return new ResponseDto<EmailDto>
                {
                    StatusCode = 201,
                    Status = true,
                    Message = "Correo enviado exitosamente",
                    Data = dto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error enviando email con cuenta {wrapper.Account.Username}: {ex.Message}");
                return new ResponseDto<EmailDto>
                {
                    StatusCode = 500,
                    Status = false,
                    Message = $"Error temporal con la cuenta {wrapper.Account.Username}",
                    Data = dto
                };
            }
        }

        // Este servicio retorna el listado de emailDto apenas evalua el data, y envia la info al servicio para reportes en segundo plano
        public async Task<ResponseDto<List<EmailDto>>> SendGradeReportPdfsAsync(EmailAllGradeDto dto)
        {
            SmtpAccountWrapper acquiredWrapper = null;
            CourseEntity courseEntity = null;
            //Esto es para la response de este servicio...
            List<EmailDto> emailInfo = [];
            var userId = _auditService.GetUserId();

            try
            {
                // Validaciones iniciales, aqui obtiene de una sola llamada el curso, settings, teacher y centro
                courseEntity = await _context.Courses
                    .Include(x => x.Units)
                    .Include(x => x.CourseSetting)
                    .Include(x => x.Center)
                        .ThenInclude(x => x.Teacher)
                    .FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.CreatedBy == userId);
                if (courseEntity is null)
                {
                    return new ResponseDto<List<EmailDto>>
                    {
                        StatusCode = 404,
                        Status = false,
                        Message = MessagesConstant.EMAIL_COURSE_NOT_REGISTERED,
                        Data = null
                    };
                }

                // Obtener datos adicionales
                var centerEntity = courseEntity.Center;
                var teacherEntity = courseEntity.Center.Teacher;
                var courseSettingEntity = courseEntity.CourseSetting;

                //Aqui declara un objeto del modelo para ingresar al canal, la lista students se poblara despues
                var emailRequest = new EmailStudentListRequest
                {
                    teacherEntity = teacherEntity,
                    courseSettingEntity = courseSettingEntity,
                    centerEntity = centerEntity,
                    courseEntity = courseEntity,
                    Content = dto.Content,
                    students = []
                };

                var studentCourseEntities = await _context.StudentsCourses
                    .Include(x => x.Student)
                    .Where(sc => sc.CourseId == dto.CourseId && sc.CreatedBy == teacherEntity.Id).ToListAsync();

                if (!studentCourseEntities.Any())
                {
                    return new ResponseDto<List<EmailDto>>
                    {
                        StatusCode = 404,
                        Status = false,
                        Message = MessagesConstant.STU_RECORD_NOT_FOUND,
                        Data = null
                    };
                }

                foreach (var item in studentCourseEntities)
                {//Una vez obtenidos los studentCourses, por cada estudiante en el curso, buscara sus student units
                    var studentUnits = await _context.StudentsUnits
                        .Where(su => su.StudentCourseId == item.Id)
                        .OrderBy(su => su.UnitNumber)
                        .ToListAsync();


                    List<StudentUnitEntity> newStudentUnits = [];


                    foreach (var unit in courseEntity.Units)
                    {

                        if (!studentUnits.Any(x => x.UnitId == unit.Id))
                        {
                            var newStudentUnit = new StudentUnitEntity
                            {
                                UnitNote = 0,
                                UnitNumber = unit.UnitNumber,
                                UnitId = unit.Id,
                                StudentCourseId = item.Id
                            };

                            newStudentUnits.Add(newStudentUnit);
                        }

                    }

                    if (newStudentUnits.Count() > 0)
                    {
                        _context.StudentsUnits.AddRange(newStudentUnits);
                        await _context.SaveChangesAsync();

                        studentUnits = await _context.StudentsUnits
                        .Where(su => su.StudentCourseId == item.Id)
                        .OrderBy(su => su.UnitNumber)
                        .ToListAsync();
                    }

                    //if (!studentUnits.Any())
                    //{
                    //    return new ResponseDto<List<EmailDto>>
                    //    {
                    //        StatusCode = 404,
                    //        Status = false,
                    //        Message = MessagesConstant.STU_RECORD_NOT_FOUND,
                    //        Data = null
                    //    };
                    //}

                    //ingresa la info de este estudiante a la lista de estudiantes del modelo...
                    emailRequest.students.Add(new EmailStudentListRequest.studentInfo
                    {
                        studentCourseEntity = item,
                        studentEntity = item.Student,
                        unitEntities = studentUnits
                    });

                    //Tambien lo ingresa dentro de la lista que retornara este dto
                    emailInfo.Add(new EmailDto
                    {
                        To = item.Student.Email,
                        Subject = $"Tus calificaciones de {courseEntity.Name} {courseEntity.Section}",
                        Content = dto.Content,
                    });
                }

                //Aqui esta ingresando el modelo creado a la lista de espera del canal, que esta siendo checkeada 24/7 por el servicio en segundo plano...
                _messageQueue.Writer.TryWrite(emailRequest);

                return new ResponseDto<List<EmailDto>>
                {
                    StatusCode = 201,
                    Status = true,
                    Message = $"Las calificaciones de de el curso '{courseEntity.Name}' fueron enviadas",
                    Data = emailInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error enviando email con PDF: {ex.Message}");
                Console.WriteLine(ex.ToString());
                return new ResponseDto<List<EmailDto>>
                {
                    StatusCode = 500,
                    Status = false,
                    Message = "Error al enviar las calificaciones",

                };
            }
            finally
            {
                if (acquiredWrapper != null)
                {
                    try
                    {
                        acquiredWrapper.Semaphore.Release();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error liberando semáforo SMTP: {ex.Message}");
                    }
                }
            }
        }


        public async Task<ResponseDto<EmailDto>> SendEmailWithPdfAsync(EmailGradeDto dto)
        {
            SmtpAccountWrapper acquiredWrapper = null;
            StudentEntity studentEntity = null;
            CourseEntity courseEntity = null;

            try
            {
                // Validaciones iniciales
                courseEntity = await _context.Courses.Include(x => x.Units).FirstOrDefaultAsync(c => c.Id == dto.CourseId);
                if (courseEntity is null)
                {
                    return new ResponseDto<EmailDto>
                    {
                        StatusCode = 404,
                        Status = false,
                        Message = MessagesConstant.EMAIL_COURSE_NOT_REGISTERED,
                        Data = null
                    };
                }

                studentEntity = await _context.Students.FirstOrDefaultAsync(s => s.Id == dto.StudentId);
                if (studentEntity is null)
                {
                    return new ResponseDto<EmailDto>
                    {
                        StatusCode = 404,
                        Status = false,
                        Message = MessagesConstant.EMAIL_STUDENT_NOT_REGISTERED,
                        Data = null
                    };
                }

                var studentCourseEntity = await _context.StudentsCourses
                    .FirstOrDefaultAsync(sc => sc.CourseId == dto.CourseId && sc.StudentId == dto.StudentId);
                if (studentCourseEntity is null)
                {
                    return new ResponseDto<EmailDto>
                    {
                        StatusCode = 404,
                        Status = false,
                        Message = MessagesConstant.EMAIL_STUDENT_NOT_REGISTERED_IN_CLASS,
                        Data = null
                    };
                }

                // Obtener datos adicionales
                var centerEntity = await _context.Centers.FirstOrDefaultAsync(c => c.Id == courseEntity.CenterId);
                var teacherEntity = await _context.Users.FirstOrDefaultAsync(t => t.Id == centerEntity.TeacherId);
                var courseSettingEntity = await _context.CoursesSettings
                    .FirstOrDefaultAsync(cs => cs.Id == courseEntity.SettingId);

                List<StudentUnitEntity> newStudentUnits = [];

                var studentUnits = await _context.StudentsUnits
                    .Where(su => su.StudentCourseId == studentCourseEntity.Id)
                    .OrderBy(su => su.UnitNumber)
                    .ToListAsync();
                foreach (var unit in courseEntity.Units)
                {

                    if (!studentUnits.Any(x => x.UnitId == unit.Id))
                    {
                        var newStudentUnit = new StudentUnitEntity
                        {
                            UnitNote = 0,
                            UnitNumber = unit.UnitNumber,
                            UnitId = unit.Id,
                            StudentCourseId = studentCourseEntity.Id
                        };

                        newStudentUnits.Add(newStudentUnit);
                    }

                }

                if (newStudentUnits.Count() > 0)
                {
                    _context.StudentsUnits.AddRange(newStudentUnits);
                    await _context.SaveChangesAsync();

                    studentUnits = await _context.StudentsUnits
                    .Where(su => su.StudentCourseId == studentCourseEntity.Id)
                    .OrderBy(su => su.UnitNumber)
                    .ToListAsync();
                }

                // Generar PDF
                var pdfBytes = await GenerateGradeReport(centerEntity, teacherEntity, courseEntity,
                    studentEntity, studentCourseEntity, courseSettingEntity, studentUnits);

                // Construir el correo
                var email = new MimeMessage();
                email.To.Add(MailboxAddress.Parse(studentEntity.Email));
                email.Subject = $"Tus calificaciones de {courseEntity.Name} {courseEntity.Section}";

                // Adjuntar PDF
                var body = new TextPart("plain") { Text = dto.Content };
                var attachment = new MimePart("application", "pdf")
                {
                    Content = new MimeContent(new MemoryStream(pdfBytes)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = $"Calificaciones_{courseEntity.Name}_{courseEntity.Section}_{studentEntity.FirstName}{studentEntity.LastName}.pdf"
                };

                var multipart = new Multipart("mixed");
                multipart.Add(body);
                multipart.Add(attachment);
                email.Body = multipart;

                // Adquirir cuenta SMTP con balanceo de carga
                acquiredWrapper = await AcquireAccountAsync();
                email.From.Add(MailboxAddress.Parse(acquiredWrapper.Account.Username));

                // Enviar el correo
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(
                    acquiredWrapper.Account.Host,
                    acquiredWrapper.Account.Port,
                    SecureSocketOptions.StartTls
                );

                await smtp.AuthenticateAsync(
                    acquiredWrapper.Account.Username,
                    acquiredWrapper.Account.Password
                );

                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return new ResponseDto<EmailDto>
                {
                    StatusCode = 201,
                    Status = true,
                    Message = $"Las calificaciones de {studentEntity.FirstName} {studentEntity.LastName} fueron enviadas",
                    Data = new EmailDto
                    {
                        To = studentEntity.Email,
                        Subject = email.Subject,
                        Content = dto.Content
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error enviando email con PDF: {ex.Message}");

                // Create minimal EmailDto for error response
                var errorEmailDto = new EmailDto
                {
                    To = studentEntity?.Email ?? string.Empty,
                    Subject = courseEntity != null
                        ? $"Calificaciones de {courseEntity.Name} - Error"
                        : "Error enviando calificaciones",
                    Content = ex.ToString()
                };

                return new ResponseDto<EmailDto>
                {
                    StatusCode = 500,
                    Status = false,
                    Message = "Error al enviar las calificaciones",
                    Data = errorEmailDto
                };
            }
            finally
            {
                if (acquiredWrapper != null)
                {
                    try
                    {
                        acquiredWrapper.Semaphore.Release();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error liberando semáforo SMTP: {ex.Message}");
                    }
                }
            }
        }

        // Generar el pdf con las calificaciones del estudiante
        public static async Task<byte[]> GenerateGradeReport(CenterEntity center, UserEntity teacher, CourseEntity course, StudentEntity student, StudentCourseEntity studentCourse, CourseSettingEntity courseSetting, List<StudentUnitEntity> studentUnits)
        {
            // Propiedades para redactar el documento PDF
            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            var document = new Document(pdf);
            var date = DateTime.Now;

            // Linea horizontal
            document.Add(new LineSeparator(new SolidLine(1f))
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(5)
                .SetMarginBottom(5));

            //// Titulo
            //var title = new Paragraph("Boletín de Calificaciones")
            //	.SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
            //	.SetFontSize(16)
            //	.SetUnderline()
            //	.SetTextAlignment(TextAlignment.CENTER);
            //document.Add(title);

            // Subtitulo
            var subtitle = new Paragraph($"{center.Name}\n{center.Abbreviation}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                .SetFontSize(14)
                .SetTextAlignment(TextAlignment.CENTER);
            document.Add(subtitle);

            var scoreType = "";

            if (Constants.ScoreTypeConstant.ARITHMETIC_SCORE == courseSetting.ScoreType)
            {
                scoreType = "Aritmética";
            }
            else if (Constants.ScoreTypeConstant.GOLD_SCORE == courseSetting.ScoreType)
            {
                scoreType = "puntos oro";
            }
            else
            {
                scoreType = "ponderada";
            }

            // Linea horizontal
            document.Add(new LineSeparator(new SolidLine(2f))
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(5)
                .SetMarginBottom(5));

            // ****** Datos generales en dos columnas con 3 filas *******
            var generalInfoTable = new Table(new float[] { 1, 1 }); // 2 columns
            generalInfoTable.SetWidth(UnitValue.CreatePercentValue(100));
            generalInfoTable.SetMarginBottom(10);

            // Row 1: Clase - Docente
            generalInfoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph()
                .Add(new Text("Clase: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)))
                .Add(new Text(course.Name)).SetFontSize(12)));

            generalInfoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph()
                .Add(new Text("Docente: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)))
                .Add(new Text($"{teacher.FirstName} {teacher.LastName}")).SetFontSize(12)));

            // Row 2: Sección - Estudiante
            generalInfoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph()
                .Add(new Text("Sección: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)))
                .Add(new Text(course.Section)).SetFontSize(12)));

            generalInfoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph()
                .Add(new Text("Estudiante: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)))
                .Add(new Text($"{student.FirstName} {student.LastName}")).SetFontSize(12)));

            // Row 3: Código - Fecha
            generalInfoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph()
                .Add(new Text("Código: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)))
                .Add(new Text(course.Code)).SetFontSize(12)));

            generalInfoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph()
                .Add(new Text("Fecha: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)))
                .Add(new Text($"{date:dd 'de' MMMM 'de' yyyy}")).SetFontSize(12)));

            generalInfoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph()
                .Add(new Text("Sistema de calificación: ").SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)))
                .Add(new Text($"{scoreType}")).SetFontSize(12)));

            document.Add(generalInfoTable);

            // Tabla de calificaciones
            Table table = new Table(new float[] { 1, 1 });
            table.SetWidth(UnitValue.CreatePercentValue(100)).SetMarginTop(5);

            // Encabezados
            string[] headers = { "Unidad", "Nota" };
            foreach (var header in headers)
            {
                var headerCell = new Cell()
                    .Add(new Paragraph(header)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                    .SetBorder(Border.NO_BORDER)
                    .SetFontSize(12);

                table.AddHeaderCell(headerCell);
            }

            // Datos dinámicos
            foreach (var unit in studentUnits)
            {
                // Agregar el número del parcial
                table.AddCell(new Cell().Add(new Paragraph(unit.UnitNumber.ToString())
                    .SetTextAlignment(TextAlignment.CENTER)));

                // Agregar la nota del parcial
                table.AddCell(new Cell().Add(new Paragraph(Math.Round((decimal)(unit.UnitNote), 2).ToString())
                    .SetTextAlignment(TextAlignment.CENTER)));
            }

            // Fila del promedio final con color
            bool passed = studentCourse.FinalNote >= courseSetting.MinimumGrade;
            var softGreen = new DeviceRgb(209, 250, 229);
            var softRed = new DeviceRgb(254, 202, 202);
            var finalRowColor = passed ? softGreen : softRed;

            // Celda de etiqueta
            table.AddCell(new Cell(1, 1)
                .Add(new Paragraph("Total / Promedio Final")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                .SetFontColor(ColorConstants.BLACK))
                .SetBackgroundColor(finalRowColor));

            // Celda de valor
            table.AddCell(new Cell(1, 1)
                .Add(new Paragraph($"{Math.Round((decimal)(studentCourse.FinalNote), 2)}%")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                .SetFontColor(ColorConstants.BLACK))
                .SetBackgroundColor(finalRowColor));

            document.Add(table);

            // *********************************************************************

            if (courseSetting.ScoreType == Constants.ScoreTypeConstant.ARITHMETIC_SCORE)
            {
                document.Add(new Paragraph("Cada unidad se califica sobre un máximo de 100 puntos.")
                    .SetFontSize(9)
                    //.SetFontColor(new DeviceRgb(169, 169, 169))
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetMarginTop(8)
                    .SetMarginBottom(6));
            }
            else if (courseSetting.ScoreType == Constants.ScoreTypeConstant.GOLD_SCORE)
            {
                document.Add(new Paragraph("La evaluación de la clase se basa en un sistema de puntos oro. El promedio total resulta de la suma de los puntajes de todas las actividades.")
                    .SetFontSize(9)
                    //.SetFontColor(new DeviceRgb(169, 169, 169))
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetMarginTop(8)
                    .SetMarginBottom(6));
            }



            string observationText = studentCourse.FinalNote >= courseSetting.MinimumGrade ? "Aprobado" : "Reprobado";

            document.Add(new Paragraph($"Observación: {observationText}")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetMarginTop(6)
                .SetMarginBottom(6));

            document.Add(new Paragraph($"El promedio mínimo de superación de clase es de {courseSetting.MinimumGrade}%")
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetMarginTop(6)
                .SetMarginBottom(10));

            if (courseSetting.ScoreType == Constants.ScoreTypeConstant.WEIGHTED_SCORE)
            {
                document.Add(new Paragraph($"Cada unidad de esta clase tiene un peso específico, representando un porcentaje distinto del total de la calificación:")
                    .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetMarginTop(6));

                foreach (var unit in course.Units.OrderBy(u => u.UnitNumber))
                {

                    document.Add(new Paragraph($"Unidad {unit.UnitNumber}:  {Math.Round((decimal)((unit.MaxScore / 100) * courseSetting.MaximumGrade), 2)} %")
                        .SetFontSize(9)
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetMarginTop(1)
                        .SetMarginBottom(1));
                }
            }

            // Linea horizontal
            document.Add(new LineSeparator(new SolidLine(1f))
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(10)
                .SetMarginBottom(5));

            // Pie de pagina
            document.Add(new Paragraph("Este reporte fue brindado por la plataforma académica ClassNotes\nPara más información comunicarse a: classnotes.service@gmail.com")
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_OBLIQUE))
                .SetMarginTop(15)
                .SetMarginBottom(15));

            // Linea horizontal
            document.Add(new LineSeparator(new SolidLine(1f))
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginTop(5)
                .SetMarginBottom(5));

            document.Close();
            return stream.ToArray();
        }
    }
}