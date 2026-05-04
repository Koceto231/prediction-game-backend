using BPFL.API.Data;
using BPFL.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.Matches
{
    public class TeamService
    {
        private readonly BPFL_DBContext bPFL_DBContext;

        public TeamService(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }
        public async Task<List<Team>> GetAllAsync()
        {
            return await bPFL_DBContext.Teams.ToListAsync();
        }

        public async Task<Team?> GetByIdAsync(int id)
        {
            var result = await bPFL_DBContext.Teams.FirstOrDefaultAsync(t => t.Id == id);
            return result;
        }
    }
}
