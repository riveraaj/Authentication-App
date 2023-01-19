using Authentication_App.Models;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using DbUpdateConcurrencyException = System.Data.Entity.Infrastructure.DbUpdateConcurrencyException;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Twitter;
using AspNet.Security.OAuth.GitHub;

namespace Authentication_App.Controllers
{
    public class HomeController : Controller
    {
        private readonly AuthenticationAppContext _context;
        private IWebHostEnvironment _environment;

        public HomeController(AuthenticationAppContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("User") != null)
            {
                return RedirectToAction(nameof(Profile));
            }
            if (TempData["Error"] != null)
            {
                ViewBag.Error = TempData["Error"].ToString();
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index([Bind("Id,Name,Email,Password,Photo,Phone,Bio")] User user)
        {
            if (!_context.Users.Any(e => e.Email == user.Email))
            {
                if (ModelState.IsValid)
                {
                    _context.Add(user);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Login));
                }
            }
            ViewBag.Error = "* Username already exist";
            return View();
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("User") != null)
            {
                return RedirectToAction(nameof(Profile));
            }
            if (TempData["Error"] != null)
            {
                ViewBag.Error = TempData["Error"].ToString();
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password)
        {
            if (_context.Users.Any(e => e.Email == email))
            {
                if (_context.Users.Where(e => e.Email.Equals(email)).Any(e => e.Password == password))
                {
                    HttpContext.Session.SetString("User", email);
                    return RedirectToAction(nameof(Profile));
                }
                ViewBag.Error = "* Email or Password do not match";
                return View();
            }
            ViewBag.Error = "* Email or Password do not match";
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Remove("User");
            await HttpContext.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        public IActionResult Profile()
        {
            if (HttpContext.Session.GetString("User") != null)
            {
                string email = HttpContext.Session.GetString("User");
                var id = _context.Users.Where(e => e.Email.Equals(email)).Select(e => e.Id);
                var user = _context.Users.Find(id.First());
                return View(user);
            }
            return RedirectToAction(nameof(Login));
        }

        public async Task<IActionResult> EditProfile(int? id)
        {
            if (HttpContext.Session.GetString("User") != null)
            {
                if (id == null || _context.Users == null)
                {
                    return RedirectToAction(nameof(Profile));
                }
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return RedirectToAction(nameof(Profile));
                }
                return View(user);
            }
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(int id, [Bind("Id,Name,Email,Password,Photo,Phone,Bio")] User user)
        {
            if (id != user.Id)
            {
                return View();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var photo = _context.Users.Where(e => e.Id == id).Select(e => e.Photo);
                    var fileName = photo.First();
                    var filelist = HttpContext.Request.Form.Files;
                    if (filelist.Count > 0)
                    {
                        foreach (var file in filelist)
                        {

                            var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                            fileName = file.FileName;
                            using var fileStream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create);
                            file.CopyTo(fileStream);
                        }
                    }
                    user.Photo = fileName;
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return View();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Profile));
            }
            return View(user);
        }

        public async Task RegisterGoogle()
        {
            await HttpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme, new AuthenticationProperties() {RedirectUri = Url.Action("GoogleResponseRegister")});
        }

        public async Task<IActionResult> GoogleResponseRegister()
        {
            var result  = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (!_context.Users.Any(e => e.Email == list[4].Value))
            {
                User user = new();
                user.Name = list[1].Value;
                user.Email = list[4].Value;
                user.Password = list[0].Value;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("User", list[4].Value);
                return RedirectToAction(nameof(Profile));
            }
            TempData["Error"] = "* Username already exist";
            return RedirectToAction(nameof(Index));
        }

        public async Task LoginGoogle()
        {
            await HttpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme, new AuthenticationProperties()
            {
                RedirectUri = Url.Action("GoogleResponseLogin")
            });
        }
        public async Task<IActionResult> GoogleResponseLogin()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (_context.Users.Any(e => e.Email == list[4].Value))
            {
                HttpContext.Session.SetString("User", list[4].Value);
                return RedirectToAction(nameof(Profile));
            }

            TempData["Error"] = "* Email or Password do not match";
            return RedirectToAction(nameof(Login));
        }

        public async Task RegisterFacebook()
        {
            await HttpContext.ChallengeAsync(FacebookDefaults.AuthenticationScheme, new AuthenticationProperties() { RedirectUri = Url.Action("FacebookResponseRegister") });
        }

        public async Task<IActionResult> FacebookResponseRegister()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (!_context.Users.Any(e => e.Email == list[1].Value))
            {
                User user = new();
                user.Name = list[2].Value;
                user.Email = list[1].Value;
                user.Password = list[0].Value;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("User", list[1].Value);
                return RedirectToAction(nameof(Profile));
            }
            TempData["Error"] = "* Username already exist";
            return RedirectToAction(nameof(Index));
        }

        public async Task LoginFacebook()
        {
            await HttpContext.ChallengeAsync(FacebookDefaults.AuthenticationScheme, new AuthenticationProperties()
            {
                RedirectUri = Url.Action("FacebookResponseLogin")
            });
        }
        public async Task<IActionResult> FacebookResponseLogin()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (_context.Users.Any(e => e.Email == list[1].Value))
            {
                HttpContext.Session.SetString("User", list[1].Value);
                return RedirectToAction(nameof(Profile));
            }

            TempData["Error"] = "* Email or Password do not match";
            return RedirectToAction(nameof(Login));
        }

        public async Task RegisterTwitter()
        {
            await HttpContext.ChallengeAsync(TwitterDefaults.AuthenticationScheme, new AuthenticationProperties() { RedirectUri = Url.Action("TwitterResponseRegister") });
        }

        public async Task<IActionResult> TwitterResponseRegister()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (!_context.Users.Any(e => e.Email == list[0].Value))
            {
                User user = new();
                user.Name = list[1].Value;
                user.Email = list[0].Value;
                user.Password = list[0].Value;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("User", list[0].Value);
                return RedirectToAction(nameof(Profile));
            }
            TempData["Error"] = "* Username already exist";
            return RedirectToAction(nameof(Index));
        }

        public async Task LoginTwitter()
        {
            await HttpContext.ChallengeAsync(FacebookDefaults.AuthenticationScheme, new AuthenticationProperties()
            {
                RedirectUri = Url.Action("TwitterResponseLogin")
            });
        }
        public async Task<IActionResult> TwitterResponseLogin()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (_context.Users.Any(e => e.Email == list[0].Value))
            {
                HttpContext.Session.SetString("User", list[0].Value);
                return RedirectToAction(nameof(Profile));
            }

            TempData["Error"] = "* Email or Password do not match";
            return RedirectToAction(nameof(Login));
        }


        public async Task RegisterGitHub()
        {
            await HttpContext.ChallengeAsync(GitHubAuthenticationDefaults.AuthenticationScheme, new AuthenticationProperties() { RedirectUri = Url.Action("GitHubResponseRegister") });
        }

        public async Task<IActionResult> GitHubResponseRegister()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (!_context.Users.Any(e => e.Email == list[0].Value))
            {
                User user = new();
                user.Name = list[2].Value;
                user.Email = list[0].Value;
                user.Password = list[0].Value;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("User", list[0].Value);
                return RedirectToAction(nameof(Profile));
            }
            TempData["Error"] = "* Username already exist";
            return RedirectToAction(nameof(Index));
        }



        public async Task LoginGitHub()
        {
            await HttpContext.ChallengeAsync(GitHubAuthenticationDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = Url.Action("GitHubResponseLogin")});
        }
        public async Task<IActionResult> GitHubResponseLogin()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            {
                claim.Value,
            });
            var list = claims.ToArray();
            if (_context.Users.Any(e => e.Email == list[0].Value))
            {
                HttpContext.Session.SetString("User", list[0].Value);
                return RedirectToAction(nameof(Profile));
            }

            TempData["Error"] = "* Email or Password do not match";
            return RedirectToAction(nameof(Login));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}