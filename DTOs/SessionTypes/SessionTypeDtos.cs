namespace MultiClinica.API.DTOs.SessionTypes;

public class CreateSessionTypeDto
{
    public string Name { get; set; } = string.Empty;
}

public class SessionTypeResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
