// Models/BulkOperation.cs
public class BulkOperation
{
    public int Id { get; set; }
    public string ActionType { get; set; } = null!; // e.g. "processing_to_pending"
    public string PerformedBy { get; set; } = null!;
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public bool RolledBack { get; set; } = false;

    public ICollection<BulkOperationItem> Items { get; set; } = new List<BulkOperationItem>();
}