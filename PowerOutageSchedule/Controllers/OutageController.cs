using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PowerOutageSchedule.Models;

namespace PowerOutageSchedule.Controllers;

public class OutageController : Controller
{
    private static readonly List<OutageSchedule> Schedules = [];

    [HttpGet]
    public IActionResult Index()
    {
        return View(Schedules);
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("", "Будь ласка, виберіть файл для імпорту.");
            return View();
        }

        try
        {
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                Schedules.Clear();

                while (await reader.ReadLineAsync() is { } line)
                {
                    if (!TryParseOutageSchedule(line, out var schedule))
                    {
                        ModelState.AddModelError("", "Неправильний формат даних.");
                        return View();
                    }

                    Schedules.Add(schedule);
                }
            }

            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Помилка імпорту даних: {ex.Message}");
            return View();
        }
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var schedule = Schedules.FirstOrDefault(s => s.GroupId == id);
        if (schedule == null)
        {
            return NotFound();
        }

        return View(schedule);
    }

    [HttpPost]
    public IActionResult Edit(OutageSchedule model)
    {
        if (!ValidateOutageTimesFormat(model.OutageTimes))
        {
            ModelState.AddModelError("", "Неправильний формат часів відключення.");
            return View(model);
        }

        var schedule = Schedules.FirstOrDefault(s => s.GroupId == model.GroupId);
        if (schedule == null)
        {
            return NotFound();
        }

        schedule.OutageTimes = model.OutageTimes;
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Export()
    {
        var json = JsonConvert.SerializeObject(Schedules, Formatting.Indented);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "schedules.json");
    }

    private bool TryParseOutageSchedule(string line, out OutageSchedule schedule)
    {
        schedule = null!;
        var parts = line.Split('.', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var groupId))
        {
            return false;
        }

        var outageTimes = parts[1].Trim();
        if (!ValidateOutageTimesFormat(outageTimes))
        {
            return false;
        }

        schedule = new OutageSchedule { GroupId = groupId, OutageTimes = outageTimes };
        return true;
    }

    private bool ValidateOutageTimesFormat(string outageTimes)
    {
        const string pattern = @"^(\d{2}:\d{2}-\d{2}:\d{2})(; \d{2}:\d{2}-\d{2}:\d{2})*$";
        return Regex.IsMatch(outageTimes, pattern);
    }
}
