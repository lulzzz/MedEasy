namespace Measures.Validators.Tests.Features.Patients
{

    using FluentAssertions;

    using FluentValidation;
    using FluentValidation.Results;

    using Measures.DataStores;
    using Measures.DTO;
    using Measures.Ids;
    using Measures.Objects;
    using Measures.Validators.Features.Patients.DTO;

    using MedEasy.Abstractions.ValueConverters;
    using MedEasy.DAL.EFStore;
    using MedEasy.DAL.Interfaces;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

    using NodaTime;
    using NodaTime.Testing;

    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    using static FluentValidation.Severity;
    using static Newtonsoft.Json.JsonConvert;

    /// <summary>
    /// Unit tests for <see cref="CreatePatientInfoValidator"/> class.
    /// </summary>
    [Feature("Measures")]
    [UnitTest]
    public class CreatePatientInfoValidatorTests : IDisposable
    {
        private ITestOutputHelper _outputHelper;
        private CreatePatientInfoValidator _validator;
        private IUnitOfWorkFactory _uowFactory;

        public CreatePatientInfoValidatorTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;

            DbContextOptionsBuilder<MeasuresStore> dbContextOptionsBuilder = new();
            dbContextOptionsBuilder.ReplaceService<IValueConverterSelector, StronglyTypedIdValueConverterSelector>()
                .UseInMemoryDatabase($"InMemory_{Guid.NewGuid()}");
            _uowFactory = new EFUnitOfWorkFactory<MeasuresStore>(dbContextOptionsBuilder.Options, (options) => new (options, new FakeClock(new Instant())));
            _validator = new CreatePatientInfoValidator(_uowFactory);
        }

        public void Dispose()
        {
            _outputHelper = null;
            _validator = null;
            _uowFactory = null;
        }

        [Fact]
        public void Should_Implements_AbstractValidator() => _validator.Should()
                .BeAssignableTo<AbstractValidator<NewPatientInfo>>();

        [Fact]
        public void Ctor_Throws_ArgumentNullException_When_Arguments_Null()
        {
            // Act
            Action action = () => new CreatePatientInfoValidator(null);

            action.Should().Throw<ArgumentNullException>().Which
                .ParamName.Should()
                .NotBeNullOrWhiteSpace();
        }

        public static IEnumerable<object[]> ValidateTestCases
        {
            get
            {
                yield return new object[]
                {
                    new NewPatientInfo(),
                    (Expression<Func<ValidationResult, bool>>)
                        (vr => !vr.IsValid
                            && vr.Errors.Count == 1
                            && vr.Errors.Once(errorItem => nameof(NewPatientInfo.Name).Equals(errorItem.PropertyName) && errorItem.Severity == Error)
                    ),
                    $"because no {nameof(NewPatientInfo)}'s data set."
                };

                yield return new object[]
                {
                    new NewPatientInfo() { Name = "Wayne" },
                    (Expression<Func<ValidationResult, bool>>) (vr => vr.IsValid),
                    $"because {nameof(NewPatientInfo.Name)} is set"
                };

                yield return new object[]
                {
                    new NewPatientInfo() { Name = "Bruce Wayne", Id = PatientId.Empty },
                    (Expression<Func<ValidationResult, bool>>)
                        (vr => !vr.IsValid
                            && vr.Errors.Count == 1
                            && vr.Errors.Once(errorItem => nameof(NewPatientInfo.Id).Equals(errorItem.PropertyName) && errorItem.Severity == Error)
                    ),
                    $"because {nameof(NewPatientInfo.Id)} is set to {PatientId.Empty}"
                };
            }
        }

        [Theory]
        [MemberData(nameof(ValidateTestCases))]
        public async Task ValidateTest(NewPatientInfo info,
            Expression<Func<ValidationResult, bool>> errorMatcher,
            string because = "")
        {
            _outputHelper.WriteLine($"{nameof(info)} : {SerializeObject(info)}");

            // Act
            ValidationResult vr = await _validator.ValidateAsync(info)
                .ConfigureAwait(false);

            // Assert
            vr.Should()
                .Match(errorMatcher, because);
        }

        [Fact]
        public async Task Should_Fails_When_Id_AlreadyExists()
        {
            // Arrange
            PatientId patientId = PatientId.New();
            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Patient>().Create(new Patient(patientId, "Grundy"));
                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            NewPatientInfo info = new()
            {
                Name = "Bruce Wayne",
                Id = patientId
            };

            // Act
            ValidationResult vr = await _validator.ValidateAsync(info);

            // Assert
            vr.IsValid.Should().BeFalse($"{nameof(Patient)} <{info.Id}> already exists");
            vr.Errors.Should()
                .HaveCount(1).And
                .Contain(x => x.PropertyName == nameof(NewPatientInfo.Id));
        }

    }
}

