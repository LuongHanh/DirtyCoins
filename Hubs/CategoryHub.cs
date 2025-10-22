using Microsoft.AspNetCore.SignalR;

namespace DirtyCoins.Hubs
{
    public class CategoryHub : Hub
    {
        private static IHubContext<CategoryHub> _context;

        public CategoryHub(IHubContext<CategoryHub> context)
        {
            _context = context;
        }

        public static void NotifyCategoryChanged()
        {
            _context?.Clients.All.SendAsync("CategoryUpdated");
        }
    }
}
