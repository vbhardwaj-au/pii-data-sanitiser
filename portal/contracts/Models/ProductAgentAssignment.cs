using System;

namespace Contracts.Models;

public class ProductAgentAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid AgentId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string? AssignedBy { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual Product? Product { get; set; }
    public virtual ObfuscationAgent? Agent { get; set; }
}