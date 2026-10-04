using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>A joining document (PAN card, certificates, cancelled cheque…) uploaded by HR or the employee, verified by HR.</summary>
public class EmployeeDocument : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DocumentType Type { get; set; }

    /// <summary>Stored file name under App_Data/documents.</summary>
    [Required, StringLength(200)] public string FileName { get; set; } = string.Empty;
    [Required, StringLength(255)] public string OriginalName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public DateTime? ReviewedAt { get; set; }
    [StringLength(500)] public string? ReviewNote { get; set; }
}
