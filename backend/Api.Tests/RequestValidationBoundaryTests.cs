using System.ComponentModel.DataAnnotations;
using Api.Dtos;
using FluentAssertions;

namespace Api.Tests;

/// <summary>
/// The per-field boundary floor, applied mechanically: null/empty, whitespace-only,
/// max length and max length + 1, on every write DTO.
/// </summary>
/// <remarks>
/// These are pure unit tests over the DataAnnotations surface — no database, no HTTP.
/// The limits live on the DTOs (Gate 3 fork B3: the approved Physical Data Model
/// declares every column as unbounded <c>text</c>), so this is the layer where the
/// boundary is actually decided.
/// </remarks>
public class RequestValidationBoundaryTests
{
    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }

    private static bool IsValid(object instance) => Validate(instance).Count == 0;

    private static string OfLength(int length) => new('x', length);

    private static Guid AnyId => Guid.NewGuid();

    // --- CreateItemRequest -------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    public void Item_name_is_required_and_rejects_null_empty_and_whitespace_only(string? name)
    {
        var request = new CreateItemRequest
        {
            Name = name!,
            AssetTag = "QA-1",
            ItemCategoryId = AnyId,
        };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CreateItemRequest.Name)));
    }

    [Fact]
    public void Item_name_accepts_exactly_the_limit_and_rejects_the_limit_plus_one()
    {
        var atLimit = new CreateItemRequest
        {
            Name = OfLength(FieldLimits.Name),
            AssetTag = "QA-2",
            ItemCategoryId = AnyId,
        };

        var overLimit = atLimit with { Name = OfLength(FieldLimits.Name + 1) };

        IsValid(atLimit).Should().BeTrue($"{FieldLimits.Name} characters is the documented maximum");
        Validate(overLimit).Should().Contain(r => r.MemberNames.Contains(nameof(CreateItemRequest.Name)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Item_asset_tag_is_required_and_rejects_null_empty_and_whitespace_only(string? assetTag)
    {
        var request = new CreateItemRequest
        {
            Name = "QA Item",
            AssetTag = assetTag!,
            ItemCategoryId = AnyId,
        };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CreateItemRequest.AssetTag)));
    }

    [Fact]
    public void Item_asset_tag_accepts_exactly_the_limit_and_rejects_the_limit_plus_one()
    {
        var atLimit = new CreateItemRequest
        {
            Name = "QA Item",
            AssetTag = OfLength(FieldLimits.AssetTag),
            ItemCategoryId = AnyId,
        };

        IsValid(atLimit).Should().BeTrue();
        Validate(atLimit with { AssetTag = OfLength(FieldLimits.AssetTag + 1) })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateItemRequest.AssetTag)));
    }

    [Fact]
    public void Item_description_is_optional_but_bounded()
    {
        var withoutDescription = new CreateItemRequest
        {
            Name = "QA Item",
            AssetTag = "QA-3",
            ItemCategoryId = AnyId,
            Description = null,
        };

        IsValid(withoutDescription).Should().BeTrue("description is optional");
        IsValid(withoutDescription with { Description = OfLength(FieldLimits.Description) }).Should().BeTrue();
        Validate(withoutDescription with { Description = OfLength(FieldLimits.Description + 1) })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateItemRequest.Description)));
    }

    // --- CreateBorrowerRequest ---------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Borrower_name_is_required_and_rejects_null_empty_and_whitespace_only(string? name)
    {
        Validate(new CreateBorrowerRequest { Name = name! })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateBorrowerRequest.Name)));
    }

    [Fact]
    public void Borrower_name_accepts_exactly_the_limit_and_rejects_the_limit_plus_one()
    {
        var atLimit = new CreateBorrowerRequest { Name = OfLength(FieldLimits.Name) };

        IsValid(atLimit).Should().BeTrue();
        Validate(atLimit with { Name = OfLength(FieldLimits.Name + 1) })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateBorrowerRequest.Name)));
    }

    [Fact]
    public void Borrower_contact_email_must_look_like_an_email_and_is_bounded()
    {
        var request = new CreateBorrowerRequest { Name = "QA Borrower" };

        IsValid(request with { ContactEmail = null }).Should().BeTrue("email is optional");
        IsValid(request with { ContactEmail = "qa@example.invalid" }).Should().BeTrue();

        Validate(request with { ContactEmail = "not-an-email" })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateBorrowerRequest.ContactEmail)));

        var atLimit = $"{OfLength(FieldLimits.Email - "@example.invalid".Length)}@example.invalid";
        atLimit.Length.Should().Be(FieldLimits.Email);
        IsValid(request with { ContactEmail = atLimit }).Should().BeTrue();

        Validate(request with { ContactEmail = $"x{atLimit}" })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateBorrowerRequest.ContactEmail)));
    }

    [Fact]
    public void Borrower_contact_phone_accepts_exactly_the_limit_and_rejects_the_limit_plus_one()
    {
        var request = new CreateBorrowerRequest { Name = "QA Borrower" };

        IsValid(request with { ContactPhone = OfLength(FieldLimits.Phone) }).Should().BeTrue();
        Validate(request with { ContactPhone = OfLength(FieldLimits.Phone + 1) })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateBorrowerRequest.ContactPhone)));
    }

    [Fact]
    public void Borrower_department_accepts_exactly_the_limit_and_rejects_the_limit_plus_one()
    {
        var request = new CreateBorrowerRequest { Name = "QA Borrower" };

        IsValid(request with { Department = OfLength(FieldLimits.Department) }).Should().BeTrue();
        Validate(request with { Department = OfLength(FieldLimits.Department + 1) })
            .Should().Contain(r => r.MemberNames.Contains(nameof(CreateBorrowerRequest.Department)));
    }

    // --- Reference data ----------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Item_category_name_is_required(string? name)
    {
        Validate(new ItemCategoryRequest { Name = name! })
            .Should().Contain(r => r.MemberNames.Contains(nameof(ItemCategoryRequest.Name)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Loan_status_name_is_required(string? name)
    {
        Validate(new LoanStatusRequest { Name = name! })
            .Should().Contain(r => r.MemberNames.Contains(nameof(LoanStatusRequest.Name)));
    }

    // --- Checkout / return -------------------------------------------------

    /// <summary>
    /// DOCUMENTS A REAL GAP, it does not paper over it.
    /// </summary>
    /// <remarks>
    /// <c>BorrowerId</c> and <c>ItemId</c> are non-nullable <see cref="Guid"/>s carrying
    /// <c>[Required]</c>. DataAnnotations' RequiredAttribute cannot distinguish "absent"
    /// from "default" on a non-nullable value type, so an omitted or all-zero id passes
    /// validation and reaches the service, which answers 404 rather than 400. The test
    /// asserts the ACTUAL behaviour; the finding is in the QA report.
    /// </remarks>
    [Fact]
    public void Checkout_request_with_empty_guids_passes_dataannotations_and_is_caught_downstream_instead()
    {
        var empty = new CheckoutRequest { BorrowerId = Guid.Empty, ItemId = Guid.Empty };

        IsValid(empty).Should().BeTrue(
            "[Required] on a non-nullable Guid can never fail — the 'missing borrower' case is "
            + "caught by the service's existence check as a 404, not by model validation as a 400");
    }

    [Fact]
    public void Return_request_with_an_empty_guid_passes_dataannotations_and_is_caught_downstream_instead()
    {
        IsValid(new ReturnLoanRequest { LoanStatusId = Guid.Empty }).Should().BeTrue();
    }
}
