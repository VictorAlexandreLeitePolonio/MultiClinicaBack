using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.SuperAdmin;

public class HubApprovalDto
{
    public bool Approved { get; set; }
    public string? Reason { get; set; }
}

public class SetClinicPlanDto
{
    public ClinicPlan Plan { get; set; }
    public bool IsFounder { get; set; }
}
