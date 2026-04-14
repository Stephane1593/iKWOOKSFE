namespace SFE.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Permissions { get; set; } = "{}"; // JSON
    public List<User> Users { get; set; } = new();
}