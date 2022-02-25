using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using timelapse.core.models;
using timelapse.infrastructure;
using Microsoft.EntityFrameworkCore;
using timelapse.api.Helpers;
using System.ComponentModel.DataAnnotations;

namespace timelapse.api.Pages
{
#pragma warning disable CS8618
#pragma warning disable CS8602

    public class ProjectEditModel : PageModel
    {
        private readonly ILogger<ProjectEditModel> _logger;
        private AppDbContext _appDbContext;
        private StorageHelper _storageHelper;

        public List<Project> projects {get;}

        public ProjectEditModel(ILogger<ProjectEditModel> logger, AppDbContext appDbContext, IConfiguration configuration)
        {
            _logger = logger;
            _appDbContext = appDbContext;
            projects = _appDbContext.Projects
                // .Include(d => d.Telemetries)
                // .Include(d => d.Images)
                // .AsSplitQuery()
                .ToList();
        }
 
        public IActionResult OnGet(int id)
        {
            var p = _appDbContext.Projects
                .FirstOrDefault(p => p.Id == id);

            if(p==null){
                return RedirectToPage("/NotFound");
            }

            ProjectName = p.Name;
            ProjectDescription = p.Description;
            ProjectId = p.Id;
            ProjectStartDate = p.StartDate;
            ProjectEndDate = p.EndDate;

            return Page();
        }

        // [BindProperty]
        // public Project Project { get; set; }

        [BindProperty] public int ProjectId { get; set; }
        [BindProperty] public string ProjectName { get; set; }
        [BindProperty] public string ProjectDescription { get; set; }
        [BindProperty, DataType(DataType.Date)] public DateTime? ProjectStartDate { get; set; }
        [BindProperty, DataType(DataType.Date)] public DateTime? ProjectEndDate { get; set; }

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            Project project = new Project(){
                Id = ProjectId,
                Name = ProjectName,
                Description = ProjectDescription,
                StartDate = ProjectStartDate.HasValue?ProjectStartDate.Value.ToUniversalTime():null,
                EndDate = ProjectEndDate.HasValue?ProjectEndDate.Value.ToUniversalTime():null
            };

            _appDbContext.Projects.Add(project);
            await _appDbContext.SaveChangesAsync();

            return RedirectToPage("./Projects");
        }
    }
//#pragma warning restore CS8618
//#pragma warning restore CS8602
}
