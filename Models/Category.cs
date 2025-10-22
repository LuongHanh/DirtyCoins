// Models/Category.cs
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DirtyCoins.Models
{
    public class Category
    {
        public int IdCategory { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        [ValidateNever] // ✅ bỏ qua khi binding
        public ICollection<Product> Products { get; set; }
    }
}
