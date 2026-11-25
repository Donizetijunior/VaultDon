namespace VaultDon.Core.Models;

public class VaultEntry
{
    public int Id { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
