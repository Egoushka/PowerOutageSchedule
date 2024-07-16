using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PowerOutageSchedule.Models;

namespace PowerOutageSchedule.Controllers;

public class OutageController : Controller
{
    private static readonly List<OutageSchedule> Schedules = [];

    [HttpGet]
    public IActionResult Index(string? filter = null)
    {
        IEnumerable<OutageSchedule> filteredSchedules = Schedules;
        bool? hasPower = null;

        if (!string.IsNullOrEmpty(filter))
        {
            if (int.TryParse(filter, out var groupId))
            {
                filteredSchedules = Schedules.Where(s => s.GroupId == groupId).ToList();
                if (filteredSchedules.Any())
                {
                    var currentTime = DateTime.Now.TimeOfDay;
                    var outageTimes = filteredSchedules.First().OutageTimes.Split(';')
                        .Select(t => t.Trim())
                        .Select(t => t.Split('-'))
                        .Select(t => new { Start = TimeSpan.Parse(t[0]), End = TimeSpan.Parse(t[1]) });

                    hasPower = !outageTimes.Any(t => currentTime >= t.Start && currentTime <= t.End);
                }
                else
                {
                    ModelState.AddModelError("", "Група не знайдена");
                }
            }
            else
            {
                ModelState.AddModelError("", "Некоректні дані");
            }
        }

        ViewBag.HasPower = hasPower;
        ViewBag.Filter = filter;
        return View(filteredSchedules);
    }


    [HttpGet]
    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is not { Length: > 0 })
        {
            ModelState.AddModelError("", "Будь ласка, виберіть файл для імпорту");
            return View();
        }

        try
        {
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                Schedules.Clear();

                while (await reader.ReadLineAsync() is { } line)
                {
                    var parts = line.Split('.');
                    if (parts.Length != 2 || !int.TryParse(parts[0], out var groupId))
                    {
                        ModelState.AddModelError("", "Неправильний формат даних");
                        return View();
                    }

                    var outageTimes = parts[1].Trim();
                    if (!ValidateOutageTimesFormat(outageTimes))
                    {
                        ModelState.AddModelError("", $"Неправильний формат часів відключення для групи {groupId}");
                        return View();
                    }

                    Schedules.Add(new OutageSchedule { GroupId = groupId, OutageTimes = outageTimes });
                }
            }

            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Помилка імпорту даних: " + ex.Message);
        }

        return View();
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
            ModelState.AddModelError("", "Неправильний формат часів відключення");
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
        var json = JsonConvert.SerializeObject(Schedules);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "schedules.json");
    }

    private bool ValidateOutageTimesFormat(string outageTimes)
    {
        const string pattern = @"^(\d{2}:\d{2}-\d{2}:\d{2})(; \d{2}:\d{2}-\d{2}:\d{2})*$";
        return Regex.IsMatch(outageTimes, pattern);
    }
}
