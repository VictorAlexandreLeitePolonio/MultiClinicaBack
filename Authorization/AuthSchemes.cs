namespace MultiClinica.API.Authorization;

/// <summary>Nomes dos esquemas de autenticação JWT.</summary>
public static class AuthSchemes
{
    /// <summary>Autenticação operacional da clínica (cookie auth_token). Esquema padrão.</summary>
    public const string ClinicAuth = "ClinicAuth";

    /// <summary>Autenticação da identidade global do paciente (cookie patient_auth_token).</summary>
    public const string PatientAuth = "PatientAuth";
}
