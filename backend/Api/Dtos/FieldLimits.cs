namespace Api.Dtos;

/// <summary>
/// Field length limits, enforced at the API boundary only.
/// </summary>
/// <remarks>
/// Gate 3 fork B3. The approved Physical Data Model declares every column as
/// unbounded <c>text</c>. Adding <c>HasMaxLength</c> would change the DDL the PDM
/// decided, and Developer does not re-derive a Gate 1 schema ruling — so the
/// limits live here, on the DTOs, where they still give QA the "max length + 1"
/// boundary cases the test checklist requires.
/// </remarks>
public static class FieldLimits
{
    public const int Name = 200;
    public const int Description = 1000;
    public const int AssetTag = 64;
    public const int Email = 256;
    public const int Phone = 32;
    public const int Department = 128;
}
