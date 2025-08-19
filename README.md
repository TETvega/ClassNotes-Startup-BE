# ClassNotes

[![SQL Server](https://img.shields.io/badge/%20SQL%20Server-Implemented-brightgreen)](https://www.microsoft.com/en-us/sql-server)
[![API REST](https://img.shields.io/badge/API%20REST-Implemented-orange)](https://restfulapi.net/)
[![.NET](https://img.shields.io/badge/.NET-Framework-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-Language-blue)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![React](https://img.shields.io/badge/React-Framework-blue)](https://reactjs.org/)
[![JavaScript](https://img.shields.io/badge/JavaScript-Language-yellow)](https://developer.mozilla.org/en-US/docs/Web/JavaScript)
[![Git](https://img.shields.io/badge/Git-Version%20Control-red)](https://git-scm.com/)

## Tabla de contenido

1. [Descripcion](#descripcion)
3. [Dependencias](#dependencias)
4. [Como instalar](#instrucciones-de-instalacion)

## Descripción

**Documentos que pueden ser de Utilidad para comprender la APLICACIÓN**
> - [Carpeta Completa:](https://drive.google.com/drive/folders/1kPBFMYXlIRq8aMxl9twHtC5yoGz2SAsU?usp=sharing)

> - [Flow APP:](#)

> - [DER:](https://drive.google.com/file/d/1l-2s2lGYKF2_S9rv7Xj0SdmgdOzKV0xX/view?usp=sharing)

**¿Que es el proyecto ClassNote?**

Este sistema está diseñado específicamente para el ámbito educativo, con el objetivo de optimizar las tareas de los docentes. Ofrece una amplia variedad de herramientas útiles, intuitivas y fáciles de aplicar, brindando una experiencia eficiente y accesible para el usuario.

**Funciones principales de backend**

Entre las funcionalidades principales que hemos implementado se encuentran:

* Gestión de información académica: Permite organizar y administrar los datos de las clases impartidas, facilitando un control centralizado y accesible.
* Manejo de tareas: Los docentes pueden asignar, calificar y editar tareas de manera ágil, promoviendo un seguimiento más efectivo del desempeño de los estudiantes.
* Control de asistencia: Incluye una funcionalidad para tomar asistencia y mantener un registro actualizado de la lista de estudiantes, lo que mejora la organización y el control en el aula.

**Objetivos de backend**

* Optimizar la estructura del backend
* Garantizar una base de datos limpia y libre de redundancias
* Gestionar archivos de forma eficiente
* Asegurar la protección y confidencialidad de los datos
* Promover la usabilidad y modularidad

## Dependencias

 * Entity Framework Core.
 * AutoMapper.
 * Microsoft.AspNetCore.Authentication.JwtBearer
 * Microsoft.AspNetCore.Identity.ENtityFramework.Core
 * Microsoft.EntityFrameworkCore.Design
 * Microsoft.EntityFrameworkCore.SqlServer
 * Microsoft.EntityFrameworkCore.Tools
 * Newtonsoft.Json

## Instrucciones de instalacion

### 1. Clonar el Repositorio

Primero, clona el repositorio en tu máquina local:

```bash
git clone https://github.com/TETvega/ClassNotes-Startup-BE.git
```
### 2. Instalación del Backend

#### Requisitos Previos

- **Visual Studio 2020** (o superior)
- **.NET 5.0** (o superior)
- **SQL Server**

#### Instrucciones

1. **Abrir el Proyecto:**

- Abre Visual Studio 2020.
- Navega al directorio donde clonaste el repositorio y abre el archivo de solución `.sln`.

2. **Restaurar Paquetes NuGet:**

- En Visual Studio, ve a **Herramientas > Administrador de paquetes NuGet > Consola del Administrador de paquetes**.
- Ejecuta el siguiente comando para restaurar todas las dependencias necesarias:


     ```bash
     Update-Package -Reinstall
     ```
***






