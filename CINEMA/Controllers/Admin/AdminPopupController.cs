using CINEMA.Controllers;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;

public class AdminPopupController : AdminBaseController
{
    private readonly CinemaContext _context;

    public AdminPopupController(CinemaContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var popups = _context.Popups.ToList();
        return View(popups);
    }
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Create(Popup popup)
    {
        _context.Popups.Add(popup);
        _context.SaveChanges();

        return RedirectToAction("Index");
    }
    public IActionResult Edit(int id)
    {
        var popup = _context.Popups.Find(id);
        return View(popup);
    }

    [HttpPost]
    public IActionResult Edit(Popup popup)
    {
        _context.Popups.Update(popup);
        _context.SaveChanges();
        return RedirectToAction("Index");
    }
    public IActionResult Delete(int id)
    {
        var popup = _context.Popups.Find(id);

        if (popup != null)
        {
            _context.Popups.Remove(popup);
            _context.SaveChanges();
        }

        return RedirectToAction("Index");
    }
    public IActionResult ViewPopup(int id)
    {
        var popup = _context.Popups.Find(id);

        if (popup == null)
        {
            return NotFound();
        }

        return View(popup);
    }
}