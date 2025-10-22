// Models/Employee.cs
using System.ComponentModel.DataAnnotations.Schema;
using static System.Formats.Asn1.AsnWriter;

namespace DirtyCoins.Models
{
    public class Employee
    {
        public int IdEmployee { get; set; }
        [ForeignKey("User")]
        public int IdUser { get; set; }
        public User User { get; set; }
        [ForeignKey("Store")]
        public int IdStore { get; set; }
        public Store Store { get; set; }

        public string FullName { get; set; }
        public string Position { get; set; }
        public DateTime HiredDate { get; set; } = DateTime.UtcNow;
    }
}
