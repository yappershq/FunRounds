namespace FunRounds.Shared;

/// <summary>
/// Fluent builder for <see cref="FunRoundDefinition"/>.
/// </summary>
public sealed class FunRoundBuilder
{
    private string  _name    = "";
    private string  _short   = "";
    private string? _primary;
    private string? _secondary;
    private bool    _knife;
    private bool    _taser;
    private bool    _heGren;
    private bool    _decoy;
    private DamageMode _damageMode = DamageMode.Any;
    private bool    _noScope;
    private int     _health = 100;

    public FunRoundBuilder WithName(string name)                { _name   = name;   return this; }
    public FunRoundBuilder WithShortName(string shortName)      { _short  = shortName; return this; }
    public FunRoundBuilder WithPrimaryWeapon(string classname)  { _primary    = classname; return this; }
    public FunRoundBuilder WithSecondaryWeapon(string classname){ _secondary  = classname; return this; }
    public FunRoundBuilder WithKnife()                          { _knife  = true;  return this; }
    public FunRoundBuilder WithTaser()                          { _taser  = true;  return this; }
    public FunRoundBuilder WithHeGrenade()                      { _heGren = true;  return this; }
    public FunRoundBuilder WithDecoy()                          { _decoy  = true;  return this; }
    public FunRoundBuilder WithHeadshotOnly()                   { _damageMode = DamageMode.HeadshotOnly; return this; }
    public FunRoundBuilder WithOneTap()                         { _damageMode = DamageMode.OneTap;       return this; }
    public FunRoundBuilder WithNoScope()                        { _noScope = true; return this; }
    public FunRoundBuilder WithHealth(int hp)                   { _health  = hp;   return this; }

    public FunRoundDefinition Build() => new()
    {
        Name            = _name,
        ShortName       = _short,
        PrimaryWeapon   = _primary,
        SecondaryWeapon = _secondary,
        Knife           = _knife,
        Taser           = _taser,
        HeGrenade       = _heGren,
        Decoy           = _decoy,
        DamageMode      = _damageMode,
        NoScope         = _noScope,
        Health          = _health,
    };
}
