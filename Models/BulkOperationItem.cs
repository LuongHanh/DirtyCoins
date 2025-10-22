// Models/BulkOperationItem.cs
public class BulkOperationItem
{
    public int Id { get; set; }
    public int BulkOperationId { get; set; }
    public BulkOperation BulkOperation { get; set; } = null!;

    public int OrderId { get; set; }
    public string OldStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
}