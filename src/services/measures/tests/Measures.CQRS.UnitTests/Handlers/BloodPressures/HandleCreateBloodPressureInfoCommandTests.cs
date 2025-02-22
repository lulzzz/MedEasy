﻿namespace Measures.CQRS.UnitTests.Handlers.BloodPressures
{
    using AutoMapper;

    using FluentAssertions;
    using FluentAssertions.Extensions;

    using Measures.DataStores;
    using Measures.CQRS.Commands.BloodPressures;
    using Measures.CQRS.Events.BloodPressures;
    using Measures.CQRS.Handlers.BloodPressures;
    using Measures.DTO;
    using Measures.Ids;
    using Measures.Mapping;
    using Measures.Objects;

    using MedEasy.CQRS.Core.Commands.Results;
    using MedEasy.DAL.EFStore;
    using MedEasy.DAL.Interfaces;
    using MedEasy.IntegrationTests.Core;

    using MediatR;

    using Moq;

    using NodaTime;
    using NodaTime.Extensions;
    using NodaTime.Testing;

    using Optional;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    using static Moq.MockBehavior;

    [UnitTest]
    [Feature("Blood pressures")]
    [Feature("Handlers")]
    public class HandleCreateBloodPressureInfoCommandTests : IClassFixture<SqliteEfCoreDatabaseFixture<MeasuresStore>>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IMediator> _mediatorMock;
        private readonly HandleCreateBloodPressureInfoCommand _sut;

        public HandleCreateBloodPressureInfoCommandTests(ITestOutputHelper outputHelper, SqliteEfCoreDatabaseFixture<MeasuresStore> databaseFixture)
        {
            _outputHelper = outputHelper;

            _uowFactory = new EFUnitOfWorkFactory<MeasuresStore>(databaseFixture.OptionsBuilder.Options, (options) =>
            {
                MeasuresStore context = new(options, new FakeClock(new Instant()));
                context.Database.EnsureCreated();
                return context;
            });
            _mapperMock = new Mock<IMapper>(Strict);
            _mediatorMock = new Mock<IMediator>(Strict);

            _sut = new HandleCreateBloodPressureInfoCommand(_uowFactory, _mapperMock.Object, _mediatorMock.Object);
        }

        public static IEnumerable<object[]> CtorThrowsArgumentNullExceptionCases
        {
            get
            {
                IUnitOfWorkFactory[] uowFactorieCases = { null, Mock.Of<IUnitOfWorkFactory>() };
                IMapper[] mapperCases = { null, Mock.Of<IMapper>() };
                IMediator[] mediatorCases = { null, Mock.Of<IMediator>() };

                IEnumerable<object[]> cases = uowFactorieCases
                    .CrossJoin(mapperCases, (uowFactory, mapper) => (uowFactory, mapper))
                    //.Where(tuple => tuple.uowFactory == null || tuple.expressionBuilder == null)
                    .Select(((IUnitOfWorkFactory uowFactory, IMapper mapper) tuple) => new { tuple.uowFactory, tuple.mapper })
                    .CrossJoin(mediatorCases, (a, mediator) => (a.uowFactory, a.mapper, mediator))
                    //.Where(tuple => tuple.uowFactory == null || tuple.expressionBuilder != null || tuple.mediator != null)
                    .Select(((IUnitOfWorkFactory uowFactory, IMapper mapper, IMediator mediator) tuple) => new { tuple.uowFactory, tuple.mapper, tuple.mediator })
                    .Where(tuple => tuple.uowFactory == null || tuple.mapper == null || tuple.mediator == null)
                    .Select(tuple => (new object[] { tuple.uowFactory, tuple.mapper, tuple.mediator }));

                return cases;
            }
        }

        [Theory]
        [MemberData(nameof(CtorThrowsArgumentNullExceptionCases))]
        public void Ctor_Throws_ArgumentNullException_When_Parameters_Is_Null(IUnitOfWorkFactory unitOfWorkFactory, IMapper mapper, IMediator mediator)
        {
            _outputHelper.WriteLine($"{nameof(unitOfWorkFactory)} is null : {unitOfWorkFactory == null}");
            _outputHelper.WriteLine($"{nameof(mapper)} is null : {mapper == null}");
            _outputHelper.WriteLine($"{nameof(mediator)} is null : {mediator == null}");
            // Act
#pragma warning disable IDE0039 // Utiliser une fonction locale
            Action action = () => new HandleCreateBloodPressureInfoCommand(unitOfWorkFactory, mapper, mediator);
#pragma warning restore IDE0039 // Utiliser une fonction locale

            // Assert
            action.Should()
                .Throw<ArgumentNullException>().Which
                .ParamName.Should()
                    .NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CreateBloodPressure_With_New_PatientInfo()
        {
            // Arrange
            CreateBloodPressureInfo newResourceInfo = new()
            {
                SystolicPressure = 120,
                DiastolicPressure = 80,
                DateOfMeasure = 23.August(2003).Add(15.Hours().Add(30.Minutes())).AsUtc().ToInstant(),
                PatientId = PatientId.New()
            };

            CreateBloodPressureInfoForPatientIdCommand cmd = new(newResourceInfo);

            _mediatorMock.Setup(mock => mock.Publish(It.IsAny<BloodPressureCreated>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Option<BloodPressureInfo, CreateCommandResult> optionalCreatedResource = await _sut.Handle(cmd, default)
                .ConfigureAwait(false);

            // Assert
            optionalCreatedResource.HasValue.Should().BeFalse();
            optionalCreatedResource.MatchNone((result) =>
            {
                result.Should()
                    .Be(CreateCommandResult.Failed_NotFound, "the patient does not exist.");
            });
            _mediatorMock.Verify(mock => mock.Publish(It.IsAny<BloodPressureCreated>(), default), Times.Never, "no measure was created");
        }

        [Fact]
        public async Task CreateBloodPressure_When_PatientInfo_Already_Exists()
        {
            // Arrange
            PatientId patientId = PatientId.New();
            Patient patientBeforeCreate = new(patientId, "Solomon Grundy");

            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Patient>().Create(patientBeforeCreate);
                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            CreateBloodPressureInfo newResourceInfo = new()
            {
                SystolicPressure = 120,
                DiastolicPressure = 80,
                DateOfMeasure = 23.August(2003).Add(15.Hours().Add(30.Minutes())).AsUtc().ToInstant(),
                PatientId = patientId
            };

            _mapperMock.Setup(mock => mock.Map<CreateBloodPressureInfo, BloodPressure>(It.IsAny<CreateBloodPressureInfo>()))
                .Returns((CreateBloodPressureInfo newMeasureInfo) => AutoMapperConfig.Build().CreateMapper().Map<CreateBloodPressureInfo, BloodPressure>(newMeasureInfo));

            _mapperMock.Setup(mock => mock.Map<BloodPressure, BloodPressureInfo>(It.IsAny<BloodPressure>()))
                .Returns((BloodPressure newMeasure) => AutoMapperConfig.Build().CreateMapper().Map<BloodPressure, BloodPressureInfo>(newMeasure));

            _mediatorMock.Setup(mock => mock.Publish(It.IsAny<BloodPressureCreated>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            CreateBloodPressureInfoForPatientIdCommand cmd = new(newResourceInfo);

            // Act
            Option<BloodPressureInfo, CreateCommandResult> optionalCreatedResource = await _sut.Handle(cmd, default)
                .ConfigureAwait(false);

            // Assert
            optionalCreatedResource.HasValue.Should()
                .BeTrue();

            optionalCreatedResource.MatchSome(async (measureInfo) =>
            {
                measureInfo.Id.Should()
                    .NotBe(BloodPressureId.Empty, $"{nameof(BloodPressureInfo.Id)} must be provided by the handler");
                measureInfo.PatientId.Should()
                    .Be(newResourceInfo.PatientId, $"resource's property '{nameof(BloodPressureInfo.PatientId)}' mus be the one carried by the command's {nameof(CreateBloodPressureInfoForPatientIdCommand.Data)}");
                measureInfo.DiastolicPressure.Should()
                    .Be(newResourceInfo.DiastolicPressure);
                measureInfo.SystolicPressure.Should()
                    .Be(newResourceInfo.SystolicPressure);

                using IUnitOfWork uow = _uowFactory.NewUnitOfWork();
                BloodPressure measureEntity = await uow.Repository<BloodPressure>()
                    .SingleAsync(x => x.Id == measureInfo.Id)
                    .ConfigureAwait(false);

                measureEntity.Id.Should()
                    .Be(measureInfo.Id);
                measureEntity.SystolicPressure.Should()
                    .Be(measureInfo.SystolicPressure);
                measureEntity.DiastolicPressure.Should()
                    .Be(measureInfo.DiastolicPressure);
                measureEntity.DateOfMeasure.Should()
                    .Be(measureInfo.DateOfMeasure);
            });

            _mediatorMock.Verify(mock => mock.Publish(It.IsAny<BloodPressureCreated>(), default), Times.Once, $"{nameof(HandleCreateBloodPressureInfoCommand)} must notify suscriber that a new measure resource was created");
            _mediatorMock.Verify(mock => mock.Publish(It.Is<BloodPressureCreated>(evt => evt.Id != default && evt.Data != default && evt.Data.SystolicPressure == cmd.Data.SystolicPressure && evt.Data.DiastolicPressure == cmd.Data.DiastolicPressure && evt.Data.DateOfMeasure == cmd.Data.DateOfMeasure), It.IsAny<CancellationToken>()),
                Times.Once, $"{nameof(HandleCreateBloodPressureInfoCommand)} must notify suscriber that a new measure resource was created");
        }
    }
}
