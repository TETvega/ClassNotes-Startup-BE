﻿using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.Otp;

namespace ClassNotes.API.Services.Otp
{
	public interface IOtpService
	{
		Task<ResponseDto<OtpGenerateResponseDto>> CreateAndSendOtpAsync(OtpCreateDto dto);
		Task<ResponseDto<OtpDto>> ValidateOtpAsync(OtpValidateDto dto);

		// Este servicio solo funciona para validar que el otp se elimina de cache
		Task<ResponseDto<OtpDto>> GetCachedOtpAsync(string email);

		string GenerateSecretKey(string hmacToUse, string id);
		string GenerateOtp(string secretKey, int otpExpirationSeconds);
	}
}