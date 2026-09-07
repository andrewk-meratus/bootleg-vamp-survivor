using Godot;

public partial class Main : Node2D
{
    private GameArena _arena;

    public override void _Ready()
    {
        _arena = new GameArena(this);
        _arena.Initialize();
    }

    public override void _Process(double deltaValue)
    {
        _arena.Update((float)deltaValue);
    }

    public override void _Draw()
    {
        _arena.Draw();
    }
}
