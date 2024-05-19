namespace Drm_Mina.Identifiers;

public abstract class IdentifierBase
{
    public abstract string Value { get; set;}
    
    public abstract bool IsValid();
    
    public abstract Field ToField();
    
}