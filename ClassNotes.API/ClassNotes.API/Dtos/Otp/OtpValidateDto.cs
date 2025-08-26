namespace ClassNotes.API.Dtos.Otp
{
    public class OtpValidateDto
    {
        public string OtpCode { get; set; }

        //Añadido para poder realizar busqueda en memoria (para testing)
        public string Email { get; set; }
    }
}