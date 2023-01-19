using System;
using System.Collections.Generic;

namespace Authentication_App.Models;

public partial class User
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Photo { get; set; }

    public int? Phone { get; set; }

    public string? Bio { get; set; }
}
