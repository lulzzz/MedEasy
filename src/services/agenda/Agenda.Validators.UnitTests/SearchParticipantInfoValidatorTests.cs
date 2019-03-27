using Agenda.DTO;
using Agenda.DTO.Resources.Search;
using FluentAssertions;
using FluentAssertions.Extensions;
using FluentValidation.Results;
using MedEasy.Abstractions;
using MedEasy.DTO.Search;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Categories;
using static FluentValidation.Severity;
using static Moq.MockBehavior;
using static Newtonsoft.Json.JsonConvert;

namespace Agenda.Validators.UnitTests
{
    [Feature("Agenda")]
    [UnitTest]
    public class SearchParticipantInfoValidatorTests : IDisposable
    {
        private static ITestOutputHelper _outputHelper;
        private SearchParticipantInfoValidator _sut;

        public SearchParticipantInfoValidatorTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
            _sut = new SearchParticipantInfoValidator();
        }

        public void Dispose() => _sut = null;

        public static IEnumerable<object[]> ValidateCases
        {
            get
            {
                yield return new object[]
                {
                    new SearchParticipantInfo(),
                    (Expression<Func<ValidationResult, bool>>)(vr => vr.IsValid),
                    "no property set"
                };

                yield return new object[]
                {
                    new SearchParticipantInfo { Page = -1 },
                    (Expression<Func<ValidationResult, bool>>)(vr => !vr.IsValid
                        && vr.Errors.Count == 1
                        && vr.Errors.Once(err => err.PropertyName == nameof(SearchParticipantInfo.Page) && err.Severity == Error)
                    ),
                    $"{nameof(SearchParticipantInfo.Page)} is negative"
                };

                yield return new object[]
                {
                    new SearchParticipantInfo { PageSize = -1 },
                    (Expression<Func<ValidationResult, bool>>)(vr => !vr.IsValid
                        && vr.Errors.Count == 1
                        && vr.Errors.Once(err => err.PropertyName == nameof(SearchParticipantInfo.PageSize) && err.Severity == Error)
                    ),
                    $"{nameof(SearchParticipantInfo.PageSize)} is negative"
                };

                

                yield return new object[]
                {
                    new SearchParticipantInfo { Sort = "-UnknownProp" },
                    (Expression<Func<ValidationResult, bool>>)(vr => !vr.IsValid
                        && vr.Errors.Count == 1
                        && vr.Errors.Once(err => err.PropertyName == nameof(SearchParticipantInfo.Sort) && err.Severity == Error
                            && "Unknown <UnknownProp> property.".Equals(err.ErrorMessage))
                    ),
                    $@"{nameof(SearchParticipantInfo.Sort)} value contains an unknown sort clause ""-UnknownProp"""
                };

                yield return new object[]
                {
                    new SearchParticipantInfo { Sort = "+UnknownProp" },
                    (Expression<Func<ValidationResult, bool>>)(vr => !vr.IsValid
                        && vr.Errors.Count == 1
                        && vr.Errors.Once(err => err.PropertyName == nameof(SearchParticipantInfo.Sort) && err.Severity == Error
                            && "Unknown <UnknownProp> property.".Equals(err.ErrorMessage))
                    ),
                    $@"{nameof(SearchParticipantInfo.Sort)} value contains an unknown sort clause ""+UnknownProp"""
                };

                yield return new object[]
                {
                    new SearchParticipantInfo { Sort = $"{nameof(ParticipantInfo.Name)},+UnknownProp" },
                    (Expression<Func<ValidationResult, bool>>)(vr => !vr.IsValid
                        && vr.Errors.Count == 1
                        && vr.Errors.Once(err => err.PropertyName == nameof(SearchParticipantInfo.Sort) && err.Severity == Error
                            && "Unknown <UnknownProp> property.".Equals(err.ErrorMessage))
                    ),
                    $@"{nameof(SearchParticipantInfo.Sort)} value contains an unknown sort clause ""+UnknownProp"""
                };

                yield return new object[]
                {
                    new SearchParticipantInfo { Sort = $"{nameof(ParticipantInfo.Name).ToLowerInvariant()}" },
                    (Expression<Func<ValidationResult, bool>>)(vr => vr.IsValid),
                    $"{nameof(SearchParticipantInfo.Sort)} properties are not case sensitive"
                };

                yield return new object[]
                {
                    new SearchParticipantInfo { Sort = $"++{nameof(ParticipantInfo.Name)}" },
                    (Expression<Func<ValidationResult, bool>>)(vr => !vr.IsValid
                        && vr.Errors.Count == 1
                        && vr.Errors.Once(err => err.PropertyName == nameof(SearchParticipantInfo.Sort) && err.Severity == Error
                            && $@"{nameof(SearchParticipantInfo.Sort)} expression ""++{nameof(ParticipantInfo.Name)}"" does not match ""{AbstractSearchInfo<ParticipantInfo>.SortPattern}"".".Equals(err.ErrorMessage))
                    ),
                    $@"{nameof(SearchParticipantInfo.Sort)} expression ""++{nameof(ParticipantInfo.Name)}"" contains two ""+"""
                };

                yield return new object[]
                {
                    new SearchParticipantInfo { Sort = $"--{nameof(ParticipantInfo.Name)}" },
                    (Expression<Func<ValidationResult, bool>>)(vr => !vr.IsValid
                        && vr.Errors.Count == 1
                        && vr.Errors.Once(err => err.PropertyName == nameof(SearchParticipantInfo.Sort) && err.Severity == Error
                            && $@"{nameof(SearchParticipantInfo.Sort)} expression ""--{nameof(ParticipantInfo.Name)}"" does not match ""{AbstractSearchInfo<ParticipantInfo>.SortPattern}"".".Equals(err.ErrorMessage))
                    ),
                    $@"{nameof(SearchParticipantInfo.Sort)} expression ""--{nameof(ParticipantInfo.Name)}"" contains two ""-"""
                };
            }
        }

        [Theory]
        [MemberData(nameof(ValidateCases))]
        public async Task ValidateSearchParticipantInfo(SearchParticipantInfo search, Expression<Func<ValidationResult, bool>> validationResultExpectation, string reason)
        {
            _outputHelper.WriteLine($"criteria : {search.Stringify()}");
            // Arrange

            // Act
            ValidationResult vr = await _sut.ValidateAsync(search)
                .ConfigureAwait(false);

            // Assert
            vr.Should()
                .Match(validationResultExpectation, reason);
        }
    }
}
