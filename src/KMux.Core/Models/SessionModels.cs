namespace KMux.Core.Models;

public class Session
{
    public Guid     Id              { get; init; } = Guid.NewGuid();
    public string   Name            { get; set;  } = "Session";
    public DateTime CreatedAt       { get; init; } = DateTime.UtcNow;
    public DateTime SavedAt         { get; set;  }
    public List<WindowLayout> Windows { get; set; } = new();
    public ShellProfile DefaultProfile { get; set; } = ShellProfile.Cmd;
}

public class SessionMeta
{
    public Guid     Id        { get; init; }
    public string   Name      { get; set;  } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime SavedAt   { get; set;  }
    public int      WindowCount { get; set; }
}

public class WindowLayout
{
    public Guid   Id        { get; init; } = Guid.NewGuid();
    public double Left      { get; set;  }
    public double Top       { get; set;  }
    public double Width     { get; set;  } = 1280;
    public double Height    { get; set;  } = 800;
    public int    ActiveTab { get; set;  } = 0;
    public List<TabLayout> Tabs { get; set; } = new();
}

public class TabLayout
{
    public Guid   Id          { get; init; } = Guid.NewGuid();
    public string Title       { get; set;  } = "Shell";
    public string RootPaneJson { get; set; } = "";   // serialized LayoutNode
    public ShellProfile? Profile { get; set; }
}
