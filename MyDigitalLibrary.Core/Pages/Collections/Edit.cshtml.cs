using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyDigitalLibrary.Core.Services;
using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;

namespace MyDigitalLibrary.Core.Pages.Collections;

public class EditModel : PageModel
{
    private readonly ICollectionService _svc;
    private readonly IBookService _bookService;

    public EditModel(ICollectionService svc, IBookService bookService)
    {
        _svc = svc;
        _bookService = bookService;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;
    [BindProperty]
    public string? Description { get; set; }
    [BindProperty]
    public bool IsSmart { get; set; }

    // rule inputs
    [BindProperty]
    public string? Status { get; set; }
    [BindProperty]
    public string? RatingOperator { get; set; }
    [BindProperty]
    public int? RatingValue { get; set; }
    [BindProperty]
    public string? Tags { get; set; }
    [BindProperty]
    public string? Series { get; set; }

    public CollectionRuleEntity[] ExistingRules { get; set; } = Array.Empty<CollectionRuleEntity>();

    public async Task OnGetAsync()
    {
        if (Id.HasValue)
        {
            var c = await _svc.GetCollectionAsync(Id.Value);
            if (c == null) { Response.StatusCode = 404; return; }
            Name = c.Name; Description = c.Description; IsSmart = c.IsSmart;
            ExistingRules = await _svc.GetRulesForCollectionAsync(c.Id);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError("Name", "Name is required.");
            return Page();
        }

        // Validate tags count if smart
        if (IsSmart && !string.IsNullOrWhiteSpace(Tags))
        {
            var tks = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray();
            if (tks.Length > 3)
            {
                ModelState.AddModelError("Tags", "At most 3 tags are allowed.");
                return Page();
            }
        }

        // Validate rating input
        if (!string.IsNullOrWhiteSpace(RatingOperator) && RatingOperator != "null")
        {
            if (!RatingValue.HasValue || RatingValue < 1 || RatingValue > 5)
            {
                ModelState.AddModelError("RatingValue", "Rating value must be between 1 and 5.");
                return Page();
            }
        }

        if (Id.HasValue)
        {
            var c = await _svc.GetCollectionAsync(Id.Value);
            if (c == null || c.UserId != userId) return Forbid();
            c.Name = Name.Trim(); c.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(); c.IsSmart = IsSmart;
            await _svc.UpdateCollectionAsync(c);

            // remove existing rules and re-create
            var existing = await _svc.GetRulesForCollectionAsync(c.Id);
            foreach (var r in existing) await _svc.RemoveRuleAsync(r.Id);
            if (IsSmart) await AddRulesFromForm(c.Id);
            return RedirectToPage("/Collections/Index");
        }

        var nc = new CollectionEntity { UserId = userId, Name = Name.Trim(), Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(), IsSmart = IsSmart };
        await _svc.CreateCollectionAsync(nc);
        if (IsSmart) await AddRulesFromForm(nc.Id);
        return RedirectToPage("/Collections/Index");
    }

    private async Task AddRulesFromForm(int collectionId)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !string.Equals(Status, "any", StringComparison.OrdinalIgnoreCase))
        {
            await _svc.AddRuleAsync(new CollectionRuleEntity { CollectionId = collectionId, RuleType = "status", RuleValue = Status!.Trim() });
        }

        if (!string.IsNullOrWhiteSpace(RatingOperator))
        {
            if (string.Equals(RatingOperator, "null", StringComparison.OrdinalIgnoreCase))
            {
                await _svc.AddRuleAsync(new CollectionRuleEntity { CollectionId = collectionId, RuleType = "rating", RuleValue = "null" });
            }
            else if (RatingValue.HasValue)
            {
                var rv = $"{RatingOperator}:{RatingValue.Value}";
                await _svc.AddRuleAsync(new CollectionRuleEntity { CollectionId = collectionId, RuleType = "rating", RuleValue = rv });
            }
        }

        if (!string.IsNullOrWhiteSpace(Tags))
        {
            var tks = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var t in tks) await _svc.AddRuleAsync(new CollectionRuleEntity { CollectionId = collectionId, RuleType = "tag", RuleValue = t });
        }

        if (!string.IsNullOrWhiteSpace(Series))
        {
            await _svc.AddRuleAsync(new CollectionRuleEntity { CollectionId = collectionId, RuleType = "series", RuleValue = Series.Trim() });
        }
    }

    // Preview rules without saving - returns top matches as JSON
    public async Task<JsonResult> OnPostPreviewAsync()
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return new JsonResult(new { error = "unauthorized" }) { StatusCode = 401 };

        if (!IsSmart) return new JsonResult(new object[0]);

        var rules = new List<Rule>();
        if (!string.IsNullOrWhiteSpace(Status) && !string.Equals(Status, "any", StringComparison.OrdinalIgnoreCase))
            rules.Add(new Rule { ColumnName = "Status", Operator = RuleOperator.Equals, Value = Status!.Trim() });

        if (!string.IsNullOrWhiteSpace(RatingOperator))
        {
            if (string.Equals(RatingOperator, "null", StringComparison.OrdinalIgnoreCase))
                rules.Add(new Rule { ColumnName = "Rating", Operator = RuleOperator.Equals, Value = "null" });
            else if (RatingValue.HasValue)
                rules.Add(new Rule { ColumnName = "Rating", Operator = RuleOperator.Equals, Value = RatingValue.Value.ToString() });
        }

        if (!string.IsNullOrWhiteSpace(Tags))
        {
            var tks = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (tks.Length > 3) return new JsonResult(new { error = "At most 3 tags allowed" }) { StatusCode = 400 };
            foreach (var t in tks) rules.Add(new Rule { ColumnName = "Tags", Operator = RuleOperator.Like, Value = t });
        }

        if (!string.IsNullOrWhiteSpace(Series))
            rules.Add(new Rule { ColumnName = "Series", Operator = RuleOperator.Like, Value = Series.Trim() });

        var matches = await _bookService.GetBooksByRulesAsync(rules.ToArray(), userId);
        var preview = matches.Take(20).Select(b => new { id = b.Id, title = b.Title, authors = b.Authors, cover = b.CoverPath, tags = b.Tags, series = b.Series }).ToArray();
        return new JsonResult(preview);
    }

    public async Task<IActionResult> OnPostDeleteRuleAsync(int ruleId)
    {
        var idClaim = User.FindFirst("userId")?.Value;
        if (!int.TryParse(idClaim, out var userId)) return RedirectToPage("/Account/Login");
        var rules = await _svc.GetRulesForCollectionAsync(Id ?? 0);
        var r = rules.FirstOrDefault(x => x.Id == ruleId);
        if (r == null) return NotFound();
        // ensure ownership via collection
        var c = await _svc.GetCollectionAsync(r.CollectionId);
        if (c == null || c.UserId != userId) return Forbid();
        await _svc.RemoveRuleAsync(ruleId);
        return RedirectToPage();
    }
}
