﻿namespace Measures.API.Tests.Features.v1.Patients
{
    using AutoMapper.QueryableExtensions;

    using Bogus;

    using DataFilters;

    using FluentAssertions;
    using FluentAssertions.Extensions;

    using Measures.API.Features.Patients;
    using Measures.API.Features.v1.BloodPressures;
    using Measures.API.Features.v1.Patients;
    using Measures.API.Routing;
    using Measures.DataStores;
    using Measures.CQRS.Commands.BloodPressures;
    using Measures.CQRS.Commands.Patients;
    using Measures.CQRS.Queries.Patients;
    using Measures.DTO;
    using Measures.Mapping;
    using Measures.Objects;

    using MedEasy.CQRS.Core.Commands;
    using MedEasy.CQRS.Core.Commands.Results;
    using MedEasy.CQRS.Core.Queries;
    using MedEasy.DAL.EFStore;
    using MedEasy.DAL.Interfaces;
    using MedEasy.DAL.Repositories;
    using MedEasy.RestObjects;

    using MediatR;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.JsonPatch;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;

    using Moq;

    using Optional;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    using static Microsoft.AspNetCore.Http.StatusCodes;
    using static Moq.MockBehavior;
    using static Newtonsoft.Json.JsonConvert;
    using static System.StringComparison;
    using static MedEasy.RestObjects.LinkRelation;
    using NodaTime;
    using NodaTime.Testing;
    using NodaTime.Extensions;
    using Measures.Ids;
    using MedEasy.Abstractions.ValueConverters;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
    using MedEasy.Ids;

    [Feature("Patients")]
    public class PatientsControllerTests : IDisposable
    {
        private Mock<LinkGenerator> _urlHelperMock;
        private PatientsController _controller;
        private ITestOutputHelper _outputHelper;
        private Mock<IOptionsSnapshot<MeasuresApiOptions>> _apiOptionsMock;
        private Mock<IMediator> _mediatorMock;
        private const string _baseUrl = "http://host/api";
        private IUnitOfWorkFactory _uowFactory;
        private static readonly ApiVersion _apiVersion = new(1, 0);

        public PatientsControllerTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;

            _urlHelperMock = new Mock<LinkGenerator>(Strict);
            _urlHelperMock.Setup(mock => mock.GetPathByAddress(It.IsAny<string>(), It.IsAny<RouteValueDictionary>(), It.IsAny<PathString>(), It.IsAny<FragmentString>(), It.IsAny<LinkOptions>()))
                .Returns((string routename, RouteValueDictionary routeValues, PathString _, FragmentString __, LinkOptions ___)
                => $"{_baseUrl}/{routename}/?{routeValues?.ToQueryString((string ____, object value) => (value as StronglyTypedId<Guid>)?.Value ?? value)}");

            DbContextOptionsBuilder<MeasuresStore> dbOptions = new();
            string dbName = $"InMemoryMedEasyDb_{Guid.NewGuid()}";
            dbOptions.ReplaceService<IValueConverterSelector, StronglyTypedIdValueConverterSelector>()
                .UseInMemoryDatabase(dbName);

            _uowFactory = new EFUnitOfWorkFactory<MeasuresStore>(dbOptions.Options, (options) => new MeasuresStore(options, new FakeClock(new Instant())));

            _apiOptionsMock = new Mock<IOptionsSnapshot<MeasuresApiOptions>>(Strict);

            _mediatorMock = new Mock<IMediator>(Strict);

            _controller = new PatientsController(
                _urlHelperMock.Object,
                _apiOptionsMock.Object,
                _mediatorMock.Object,
                _apiVersion);
        }

        public void Dispose()
        {
            _urlHelperMock = null;
            _controller = null;
            _outputHelper = null;
            _apiOptionsMock = null;
            _mediatorMock = null;
            _uowFactory = null;
        }

        public static IEnumerable<object> GetLastBloodPressuresMesuresCases
        {
            get
            {
                Faker faker = new();
                yield return new object[]
                {
                    Enumerable.Empty<BloodPressure>(),
                    new GetMostRecentPhysiologicalMeasuresInfo { PatientId = PatientId.New(), Count = 10 },
                    (Expression<Func<IEnumerable<BloodPressureInfo>, bool>>) (x => !x.Any())
                };

                yield return new object[]
                {
                    new []
                    {
                        new BloodPressure(id:BloodPressureId.New(),
                                          patientId: PatientId.New(),
                                          dateOfMeasure: faker.Noda().Instant.Recent(),
                                          systolicPressure: 120,
                                          diastolicPressure: 80)
                    },
                    new GetMostRecentPhysiologicalMeasuresInfo { PatientId = PatientId.New(), Count = 10 },

                    (Expression<Func<IEnumerable<BloodPressureInfo>, bool>>) (x => !x.Any())
                };

                {
                    PatientId patientId = PatientId.New();
                    yield return new object[]
                    {
                        new []
                        {
                           new BloodPressure(patientId, BloodPressureId.New(), dateOfMeasure: faker.Noda().Instant.Recent(), systolicPressure: 120, diastolicPressure: 80)
                        },
                        new GetMostRecentPhysiologicalMeasuresInfo { PatientId = patientId, Count = 10 },
                        (Expression<Func<IEnumerable<BloodPressureInfo>, bool>>) (x => x.All(measure => measure.PatientId == patientId) && x.Exactly(1))
                    };
                }
            }
        }

        public static IEnumerable<object[]> GetMostRecentTemperaturesMeasuresCases
        {
            get
            {
                yield return new object[]
                {
                    Enumerable.Empty<Temperature>(),
                    new GetMostRecentPhysiologicalMeasuresInfo { PatientId = PatientId.New(), Count = 10 },
                    (Expression<Func<IEnumerable<TemperatureInfo>, bool>>) (x => !x.Any())
                };

                yield return new object[]
                {
                    new []
                    {
                        new Temperature(TemperatureId.New(), PatientId.New(), dateOfMeasure: 18.August(2003).AsUtc().ToInstant(), value : 37)
                    },
                    new GetMostRecentPhysiologicalMeasuresInfo { PatientId = PatientId.New(), Count = 10 },
                    (Expression<Func<IEnumerable<TemperatureInfo>, bool>>) (x => !x.Any())
                };
                {
                    PatientId patientId = PatientId.New();
                    yield return new object[]
                    {
                        new []
                        {
                            new Temperature(TemperatureId.New(), patientId, dateOfMeasure: 18.August(2003).AsUtc().ToInstant(), value : 37)
                        },
                        new GetMostRecentPhysiologicalMeasuresInfo { PatientId = patientId, Count = 10 },
                        (Expression<Func<IEnumerable<TemperatureInfo>, bool>>) (x => x.All(measure => measure.PatientId == patientId) && x.Count() == 1)
                    };
                }
            }
        }

        public static IEnumerable<object[]> GetAllTestCases
        {
            get
            {
                int[] pageSizes = { 1, 10, 20 };
                int[] pages = { 1, 5, 10 };

                foreach (int pageSize in pageSizes)
                {
                    foreach (int page in pages)
                    {
                        yield return new object[]
                        {
                            Enumerable.Empty<Patient>(), // Current store state
                            (pageSize, page), // request,
                            (defaultPageSize : 30, maxPageSize : 200),
                            0,    //expected total
                            (
                                first : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == First
                                    &&
                                        ($"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?" +
                                        $"Controller={PatientsController.EndpointName}" +
                                        $"&page=1" +
                                        $"&pageSize={(pageSize < 1 ? 1 : Math.Min(pageSize, 200))}&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                                previous : (Expression<Func<Link, bool>>) (x => x == null), // expected link to previous page
                                next :(Expression<Func<Link, bool>>) (x => x == null), // expected link to next page
                                last : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Last
                                    &&
                                        ($"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?" +
                                        $"Controller={PatientsController.EndpointName}" +
                                        $"&page=1" +
                                        $"&pageSize={(pageSize < 1 ? 1 : Math.Min(pageSize, 200))}&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase))
                            )  // expected link to last page
                        };
                    }
                }

                Faker<Patient> patientFaker = new Faker<Patient>()
                    .CustomInstantiator(faker =>
                    {
                        Patient patient = new(PatientId.New(), faker.Person.FullName);

                        return patient;
                    });
                {
                    IEnumerable<Patient> items = patientFaker.Generate(400);

                    yield return new object[]
                    {
                        items,
                        (pageSize : PaginationConfiguration.DefaultPageSize, page : 1), // request
                        (defaultPageSize : 30, maxPageSize : 200),
                        400,    //expected total
                        (
                            first : (Expression<Func<Link, bool>>) (x => x != null
                                                                         && x.Relation == First
                                                                         && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={PatientsController.EndpointName}&page=1&pageSize={PaginationConfiguration.DefaultPageSize}&version={_apiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                            previous : (Expression<Func<Link, bool>>) (x => x == null), // expected link to previous page
                            next : (Expression<Func<Link, bool>>) (x => x != null
                                                                        && x.Relation == Next
                                                                        && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={PatientsController.EndpointName}&page=2&pageSize={PaginationConfiguration.DefaultPageSize}&version={_apiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to next page
                            last : (Expression<Func<Link, bool>>) (x => x != null
                                                                        && x.Relation == Last
                                                                        && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={PatientsController.EndpointName}&page=14&pageSize={PaginationConfiguration.DefaultPageSize}&version={_apiVersion}".Equals(x.Href, OrdinalIgnoreCase))
                        )  // expected link to last page
                    };
                }
                {
                    IEnumerable<Patient> items = patientFaker.Generate(400);

                    yield return new object[]
                    {
                        items,
                        (pageSize : 10, page : 1), // request
                        (defaultPageSize : 30, maxPageSize : 200),
                        400,    //expected total
                        (
                            first : (Expression<Func<Link, bool>>) (x => x != null
                                                                         && x.Relation == First
                                                                         && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={PatientsController.EndpointName}&page=1&pageSize=10&version={_apiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                            previous : (Expression<Func<Link, bool>>) (x => x == null), // expected link to previous page
                            next : (Expression<Func<Link, bool>>) (x => x != null
                                                                        && x.Relation == Next
                                                                        && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={PatientsController.EndpointName}&page=2&pageSize=10&version={_apiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to next page
                            last : (Expression<Func<Link, bool>>) (x => x != null
                                                                        && x.Relation == Last
                                                                        && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={PatientsController.EndpointName}&page=40&pageSize=10&version={_apiVersion}".Equals(x.Href, OrdinalIgnoreCase))  // expected link to last page
                        )
                    };
                }

                {
                    Patient patient = new(PatientId.New(), "Bruce Wayne");
                    yield return new object[]
                    {
                        new [] { patient },
                        (pageSize : PaginationConfiguration.DefaultPageSize, page : 1), // request
                        (defaultPageSize : 30, maxPageSize : 200),
                        1,    //expected total
                        (
                            first : (Expression<Func<Link, bool>>) (x => x != null
                                && x.Relation == First
                                && ($"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?" +
                                    $"Controller={PatientsController.EndpointName}" +
                                    "&page=1" +
                                    $"&pageSize={PaginationConfiguration.DefaultPageSize}&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                            previous :(Expression<Func<Link, bool>>) (x => x == null), // expected link to previous page
                            next : (Expression<Func<Link, bool>>) (x => x == null), // expected link to next page
                            last : (Expression<Func<Link, bool>>) (x => x != null
                                                                        && x.Relation == Last
                                                                        && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={PatientsController.EndpointName}&page=1&pageSize={PaginationConfiguration.DefaultPageSize}&version={_apiVersion}".Equals(x.Href, OrdinalIgnoreCase))
                        ), // expected link to last page
                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetAllTestCases))]
        public async Task GetAll(IEnumerable<Patient> items, (int pageSize, int page) request,
            (int defaultPageSize, int maxPageSize) pagingOptions,
            int expectedCount,
            (Expression<Func<Link, bool>> firstPageUrlExpectation, Expression<Func<Link, bool>> previousPageUrlExpectation, Expression<Func<Link, bool>> nextPageUrlExpectation, Expression<Func<Link, bool>> lastPageUrlExpectation) linksExpectation)
        {
            _outputHelper.WriteLine($"Testing {nameof(PatientsController.Get)}({nameof(PaginationConfiguration)})");
            _outputHelper.WriteLine($"Page size : {request.pageSize}");
            _outputHelper.WriteLine($"Page : {request.page}");
            _outputHelper.WriteLine($"specialties store count: {items.Count()}");

            // Arrange
            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Patient>().Create(items);
                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<GetPageOfPatientInfoQuery>(), It.IsAny<CancellationToken>()))
                .Returns((GetPageOfPatientInfoQuery query, CancellationToken cancellationToken) =>
                {
                    using IUnitOfWork uow = _uowFactory.NewUnitOfWork();
                    Expression<Func<Patient, PatientInfo>> selector = AutoMapperConfig.Build().ExpressionBuilder.GetMapExpression<Patient, PatientInfo>();
                    return uow.Repository<Patient>()
                              .ReadPageAsync(
                                    selector,
                                    query.Data.PageSize,
                                    query.Data.Page,
                                    new Sort<PatientInfo>(nameof(PatientInfo.UpdatedDate)),
                                    cancellationToken)
                              .AsTask();
                });

            _apiOptionsMock.SetupGet(mock => mock.Value).Returns(new MeasuresApiOptions { DefaultPageSize = pagingOptions.defaultPageSize, MaxPageSize = pagingOptions.maxPageSize });

            // Act
            IActionResult actionResult = await _controller.Get(new PaginationConfiguration { Page = request.page, PageSize = request.pageSize })
                .ConfigureAwait(false);

            // Assert
            _apiOptionsMock.VerifyGet(mock => mock.Value, Times.Once, $"because {nameof(PatientsController)}.{nameof(PatientsController.Get)} must always check that {nameof(PaginationConfiguration.PageSize)} don't exceed {nameof(MeasuresApiOptions.MaxPageSize)} value");
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<GetPageOfPatientInfoQuery>(), It.IsAny<CancellationToken>()), Times.Once);

            actionResult.Should()
                    .NotBeNull().And
                    .BeOfType<OkObjectResult>();
            ObjectResult okObjectResult = (OkObjectResult)actionResult;

            object value = okObjectResult.Value;

            okObjectResult.Value.Should()
                    .NotBeNull().And
                    .BeAssignableTo<GenericPagedGetResponse<Browsable<PatientInfo>>>();

            GenericPagedGetResponse<Browsable<PatientInfo>> response = (GenericPagedGetResponse<Browsable<PatientInfo>>)value;

            _outputHelper.WriteLine($"response : {response}");

            response.Items.Should()
                .NotBeNull();

            if (response.Items.Any())
            {
                response.Items.Should()
                    .NotContainNulls().And
                    .OnlyContain(x => x.Links.Once(link => link.Relation == Self));
            }

            response.Total.Should()
                    .Be(expectedCount, $@"because the ""{nameof(GenericPagedGetResponse<PatientInfo>)}.{nameof(GenericPagedGetResponse<PatientInfo>.Total)}"" property indicates the number of elements");

            response.Links.First.Should().Match(linksExpectation.firstPageUrlExpectation);
            response.Links.Previous.Should().Match(linksExpectation.previousPageUrlExpectation);
            response.Links.Next.Should().Match(linksExpectation.nextPageUrlExpectation);
            response.Links.Last.Should().Match(linksExpectation.lastPageUrlExpectation);
        }

        public static IEnumerable<object[]> SearchCases
        {
            get
            {
                {
                    SearchPatientInfo searchInfo = new()
                    {
                        Name = "bruce",
                        Page = 1,
                        PageSize = 30,
                        Sort = "-birthdate"
                    };

                    yield return new object[]
                    {
                        Enumerable.Empty<Patient>(),
                        searchInfo,
                        (
                        (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == First
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"Controller={PatientsController.EndpointName}" +
                                $"&name={searchInfo.Name}"+
                                $"&page=1&pageSize=30" +
                                $"&sort={searchInfo.Sort}&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        (Expression<Func<Link, bool>>)(previous => previous == null),
                        (Expression<Func<Link, bool>>)(next => next == null),
                        (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == Last
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"Controller={PatientsController.EndpointName}" +
                                $"&name={searchInfo.Name}"+
                                $"&page=1" +
                                $"&pageSize=30" +
                                $"&sort={searchInfo.Sort}&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)))
                    };
                }
                {
                    SearchPatientInfo searchInfo = new()
                    {
                        Name = "!wayne",
                        Page = 1,
                        PageSize = 30,
                        Sort = "-birthdate"
                    };
                    Patient patient = new(PatientId.New(), "Bruce wayne");

                    yield return new object[]
                    {
                        new [] { patient },
                        searchInfo,
                        (
                           (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == First
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"Controller={PatientsController.EndpointName}" +
                                $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                $"&page=1&pageSize=30" +
                                $"&sort={searchInfo.Sort}&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)),
                            (Expression<Func<Link, bool>>)(previous => previous == null),
                            (Expression<Func<Link, bool>>)(next => next == null),
                            (Expression<Func<Link, bool>>) (x => x != null
                                && x.Relation == Last
                                && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                    $"Controller={PatientsController.EndpointName}" +
                                    $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                    $"&page=1&pageSize=30" +
                                    $"&sort={searchInfo.Sort}&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase))
                        )
                    };
                }
                {
                    SearchPatientInfo searchInfo = new()
                    {
                        Name = "bruce",
                        Page = 1,
                        PageSize = 30,
                    };
                    Patient patient = new(PatientId.New(), "Bruce");

                    yield return new object[]
                    {
                        new [] { patient },
                        searchInfo,
                        (
                            (Expression<Func<Link, bool>>) (x => x != null
                                && x.Relation == First
                                && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                    $"Controller={PatientsController.EndpointName}" +
                                    $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                    $"&page=1&pageSize=30&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                            (Expression<Func<Link, bool>>)(previous => previous == null),
                            (Expression<Func<Link, bool>>)(next => next == null),
                            (Expression<Func<Link, bool>>) (x => x != null
                                && x.Relation == Last
                                && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                    $"Controller={PatientsController.EndpointName}" +
                                    $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                    $"&page=1&pageSize=30&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase))
                        )

                    };
                }

                {
                    SearchPatientInfo searchInfo = new()
                    {
                        Name = "bruce",
                        Page = 1,
                        PageSize = 30,
                        BirthDate = 31.July(2010).ToLocalDateTime().Date
                    };
                    Patient patient = new Patient(PatientId.New(), "Bruce wayne")
                        .WasBornOn(31.July(2010).ToLocalDateTime().Date);

                    yield return new object[]
                    {
                        new [] { patient },
                        searchInfo,
                        ( (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == First
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"birthdate={searchInfo.BirthDate.Value:R}" +
                                $"&Controller={PatientsController.EndpointName}" +
                                $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                $"&page=1&pageSize=30&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        (Expression<Func<Link, bool>>)(previous => previous == null),
                        (Expression<Func<Link, bool>>)(next => next == null),
                        (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == Last
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"birthdate={searchInfo.BirthDate.Value:R}" +
                                $"&Controller={PatientsController.EndpointName}" +
                                $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                $"&page=1&pageSize=30&version={_apiVersion}").Equals(x.Href, OrdinalIgnoreCase)))

                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(SearchCases))]
        public async Task Search(IEnumerable<Patient> entries, SearchPatientInfo searchRequest,
        (Expression<Func<Link, bool>> firstPageLink, Expression<Func<Link, bool>> previousPageLink, Expression<Func<Link, bool>> nextPageLink, Expression<Func<Link, bool>> lastPageLink) linksExpectation)
        {
            _outputHelper.WriteLine($"Entries : {SerializeObject(entries)}");
            _outputHelper.WriteLine($"Request : {SerializeObject(searchRequest)}");

            // Arrange
            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Patient>().Create(entries);
                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<SearchQuery<PatientInfo>>(), It.IsAny<CancellationToken>()))
                .Returns((SearchQuery<PatientInfo> query, CancellationToken cancellationToken) =>
                {
                    using IUnitOfWork uow = _uowFactory.NewUnitOfWork();
                    Expression<Func<Patient, PatientInfo>> selector = AutoMapperConfig.Build().ExpressionBuilder.GetMapExpression<Patient, PatientInfo>();

                    Expression<Func<PatientInfo, bool>> filter = query.Data.Filter?.ToExpression<PatientInfo>() ?? (_ => true);

                    return uow.Repository<Patient>()
                              .WhereAsync(
                                    selector,
                                    filter,
                                    query.Data.Sort,
                                    query.Data.PageSize,
                                    query.Data.Page,
                                    cancellationToken)
                              .AsTask();
                });

            // Act
            IActionResult actionResult = await _controller.Search(searchRequest, default)
                    .ConfigureAwait(false);

            // Assert
            GenericPagedGetResponse<Browsable<PatientInfo>> content = actionResult.Should()
                .NotBeNull().And
                .BeOfType<OkObjectResult>().Which
                    .Value.Should()
                        .NotBeNull().And
                        .BeAssignableTo<GenericPagedGetResponse<Browsable<PatientInfo>>>().Which;

            content.Items.Should()
                .NotBeNull($"{nameof(GenericPagedGetResponse<object>.Items)} must not be null").And
                .NotContainNulls($"{nameof(GenericPagedGetResponse<object>.Items)} must not contains null").And
                .NotContain(x => x.Resource == null).And
                .NotContain(x => x.Links == null);

            content.Links.Should()
                .NotBeNull();
            PageLinks links = content.Links;

            _outputHelper.WriteLine($"Links : {links.Jsonify()}");

            links.First.Should().Match(linksExpectation.firstPageLink);
            links.Previous.Should().Match(linksExpectation.previousPageLink);
            links.Next.Should().Match(linksExpectation.nextPageLink);
            links.Last.Should().Match(linksExpectation.lastPageLink);
        }

        [Fact]
        public async Task GivenMediatorReturnsEmptyPage_Search_Returns_NotFound_When_Requesting_PageTwoOfResult()
        {
            // Arrange
            SearchPatientInfo searchRequest = new()
            {
                Page = 2,
                PageSize = 10,
                Name = "*e*"
            };

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<SearchQuery<PatientInfo>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Page<PatientInfo>.Empty(pageSize: 10));

            // Act
            IActionResult actionResult = await _controller.Search(searchRequest, default)
                    .ConfigureAwait(false);

            // Assert
            actionResult.Should()
                .NotBeNull().And
                .BeAssignableTo<NotFoundResult>();
        }

        public static IEnumerable<object> PatchCases
        {
            get
            {
                {
                    JsonPatchDocument<PatientInfo> patchDocument = new();
                    patchDocument.Replace(x => x.Name, "Bruce");
                    PatientId patientId = PatientId.New();
                    yield return new object[]
                    {
                        new Patient(PatientId.New(), "John Doe"),
                        patchDocument,
                        (Expression<Func<Patient, bool>>)(x => x.Id == patientId && x.Name == "Bruce")
                    };
                }
            }
        }

        [Fact]
        public async Task GetWithUnknownIdShouldReturnNotFound()
        {
            _mediatorMock.Setup(mock => mock.Send(It.IsAny<GetPatientInfoByIdQuery>(), It.IsAny<CancellationToken>()))
                .Returns(async (GetPatientInfoByIdQuery query, CancellationToken ct) =>
                {
                    using IUnitOfWork uow = _uowFactory.NewUnitOfWork();
                    Expression<Func<Patient, PatientInfo>> selector = AutoMapperConfig.Build().ExpressionBuilder
.GetMapExpression<Patient, PatientInfo>();

                    return await uow.Repository<Patient>().SingleOrDefaultAsync(
                        selector,
                        (Patient x) => x.Id == query.Data,
                        ct)
                        .ConfigureAwait(false);
                });

            //Act
            IActionResult actionResult = await _controller.Get(PatientId.New())
                .ConfigureAwait(false);

            //Assert
            actionResult.Should()
                .NotBeNull().And
                .BeOfType<NotFoundResult>().Which
                    .StatusCode.Should().Be(404);

            _mediatorMock.Verify();
        }

        [Fact]
        public async Task Get()
        {
            //Arrange
            Patient patient = new(PatientId.New(), "Bruce Wayne");
            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Patient>().Create(patient);
                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
            PatientInfo expectedResource = new()
            {
                Id = patient.Id,
                Name = "Bruce Wayne"
            };

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<GetPatientInfoByIdQuery>(), It.IsAny<CancellationToken>()))
                .Returns(async (GetPatientInfoByIdQuery query, CancellationToken ct) =>
               {
                   using IUnitOfWork uow = _uowFactory.NewUnitOfWork();
                   Expression<Func<Patient, PatientInfo>> selector = AutoMapperConfig.Build().ExpressionBuilder.GetMapExpression<Patient, PatientInfo>();

                   return await uow.Repository<Patient>().SingleOrDefaultAsync(selector, (Patient x) => x.Id == query.Data, ct)
                       .ConfigureAwait(false);
               });

            //Act
            IActionResult actionResult = await _controller.Get(patient.Id)
                .ConfigureAwait(false);

            //Assert

            Browsable<PatientInfo> result = actionResult.Should()
                .NotBeNull().And
                .BeOfType<OkObjectResult>().Which
                    .Value.Should()
                    .BeAssignableTo<Browsable<PatientInfo>>().Which;

            IEnumerable<Link> links = result.Links;

            links.Should()
                .NotBeNull().And
                .NotContainNulls().And
                .NotContain(x => string.IsNullOrWhiteSpace(x.Relation)).And
                .NotContain(x => string.IsNullOrWhiteSpace(x.Href), $"{nameof(Browsable<PatientInfo>)}{nameof(Browsable<PatientInfo>.Links)} cannot contain any element " +
                    $"with null/empty/whitespace {nameof(Link.Href)}s").And
                .ContainSingle(x => x.Relation == Self).And
                .ContainSingle(x => x.Relation == "delete").And
                .ContainSingle(x => x.Relation == BloodPressuresController.EndpointName.Slugify());

            Link self = links.Single(x => x.Relation == Self);
            self.Href.Should()
                .NotBeNullOrWhiteSpace().And
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultGetOneByIdApi}/?Controller={PatientsController.EndpointName}&{nameof(PatientInfo.Id)}={expectedResource.Id.Value}&version={_apiVersion}");
            self.Relation.Should()
                .NotBeNullOrWhiteSpace()
                .And.BeEquivalentTo(Self);
            self.Method.Should()
                .Be("GET");

            Link linkDelete = links.Single(x => x.Relation == "delete");
            linkDelete.Href.Should()
                .NotBeNullOrWhiteSpace().And
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultGetOneByIdApi}/?Controller={PatientsController.EndpointName}&{nameof(PatientInfo.Id)}={expectedResource.Id.Value}&version={_apiVersion}");
            linkDelete.Method.Should().Be("DELETE");

            Link bloodPressuresLink = links.Single(x => x.Relation == BloodPressuresController.EndpointName.Slugify());
            bloodPressuresLink.Href.Should()
                .NotBeNullOrWhiteSpace().And
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?Controller={BloodPressuresController.EndpointName}&{nameof(BloodPressureInfo.PatientId)}={expectedResource.Id.Value}&version={_apiVersion}");
            bloodPressuresLink.Method.Should().Be("GET");

            PatientInfo actualResource = result.Resource;
            actualResource.Should().NotBeNull();
            actualResource.Id.Should().Be(expectedResource.Id);
            actualResource.Name.Should().Be(expectedResource.Name);

            _urlHelperMock.Verify();
            _mediatorMock.Verify();
        }

        [Fact]
        public async Task WhenMediatorReturnsNotFound_Delete_Returns_NotFound()
        {
            // Arrange
            PatientId idToDelete = PatientId.New();

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<DeletePatientInfoByIdCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DeleteCommandResult.Failed_NotFound);

            // Act
            IActionResult actionResult = await _controller.Delete(id: idToDelete, ct: default)
                .ConfigureAwait(false);

            // Assert
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<DeletePatientInfoByIdCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediatorMock.Verify(mock => mock.Send(It.Is<DeletePatientInfoByIdCommand>(cmd => cmd.Data == idToDelete), It.IsAny<CancellationToken>()), Times.Once);

            actionResult.Should()
                .BeAssignableTo<NotFoundResult>();
        }

        [Fact]
        public async Task WhenMediatorReturnsSuccess_Delete_Returns_NoContent()
        {
            // Arrange
            PatientId idToDelete = PatientId.New();

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<DeletePatientInfoByIdCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DeleteCommandResult.Done);

            // Act
            IActionResult actionResult = await _controller.Delete(id: idToDelete, ct: default)
                .ConfigureAwait(false);

            // Assert
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<DeletePatientInfoByIdCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediatorMock.Verify(mock => mock.Send(It.Is<DeletePatientInfoByIdCommand>(cmd => cmd.Data == idToDelete), It.IsAny<CancellationToken>()), Times.Once);

            actionResult.Should()
                .BeAssignableTo<NoContentResult>();
        }

        [Fact]
        public async Task GivenMediatorReturnsNone_GetBloodPressures_ReturnsNotFound()
        {
            // Arrange
            PatientId patientId = PatientId.New();
            PaginationConfiguration pagination = new() { Page = 1, PageSize = 50 };

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<GetPatientInfoByIdQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Option.None<PatientInfo>());

            // Act
            IActionResult actionResult = await _controller.GetBloodPressures(id: patientId, pagination: pagination, ct: default)
                .ConfigureAwait(false);

            // Assert
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<GetPatientInfoByIdQuery>(), It.IsAny<CancellationToken>()), Times.Once);

            actionResult.Should()
                .BeAssignableTo<NotFoundResult>();
        }

        public static IEnumerable<object[]> GetBloodPressuresWhenPatientExistsCases
        {
            get
            {
                yield return new object[]
                {
                    (page :1, pageSize:10),
                    (defaultPageSize : 30, maxPageSize : 200)
                };

                yield return new object[]
                {
                    (page :1, pageSize:10),
                    (defaultPageSize : 30, maxPageSize : 5)
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetBloodPressuresWhenPatientExistsCases))]
        public async Task GivenMediatorReturnsSome_GetBloodPressures_RedirectToSearch((int page, int pageSize) pagination, (int defaultPageSize, int maxPageSize) pagingConfiguration)
        {
            // Arrange
            PaginationConfiguration paging = new()
            {
                Page = pagination.page,
                PageSize = pagination.pageSize
            };
            PatientId patientId = PatientId.New();

            MeasuresApiOptions apiOptions = new()
            {
                DefaultPageSize = pagingConfiguration.defaultPageSize,
                MaxPageSize = pagingConfiguration.maxPageSize
            };
            _apiOptionsMock.Setup(mock => mock.Value).Returns(apiOptions);

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<GetPatientInfoByIdQuery>(), It.IsAny<CancellationToken>()))
                    .Returns((GetPatientInfoByIdQuery query, CancellationToken _) => new ValueTask<Option<PatientInfo>>(new PatientInfo
                    {
                        Id = query.Data
                    }.Some()).AsTask());

            // Act
            IActionResult actionResult = await _controller.GetBloodPressures(id: patientId, pagination: paging, ct: default)
                .ConfigureAwait(false);

            // Assert
            _apiOptionsMock.Verify(mock => mock.Value, Times.Once);
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<GetPatientInfoByIdQuery>(), It.IsAny<CancellationToken>()), Times.Once);

            RedirectToRouteResult redirect = actionResult.Should()
                .BeAssignableTo<RedirectToRouteResult>().Which;

            redirect.RouteName.Should()
                            .Be(RouteNames.DefaultSearchResourcesApi);
            redirect.PreserveMethod.Should().BeTrue();
            redirect.Permanent.Should().BeFalse();
            redirect.RouteValues.Should()
                        .ContainKey("controller").And
                        .ContainKey("patientId").And
                        .ContainKey("page").And
                        .ContainKey("pageSize");

            redirect.RouteValues["controller"].Should()
                .Be(BloodPressuresController.EndpointName);
            redirect.RouteValues["patientId"].Should()
                        .Be(patientId);
            redirect.RouteValues["page"].Should()
                        .Be(pagination.page);
            redirect.RouteValues["pageSize"].Should()
                        .Be(Math.Min(pagination.pageSize, apiOptions.MaxPageSize), "request pageSize must be capped by the controller");
        }

        [Fact]
        public async Task Post_BloodPressure_For_Patient()
        {
            // Arrange
            NewBloodPressureModel newMeasure = new()
            {
                SystolicPressure = 120,
                DiastolicPressure = 80,
                DateOfMeasure = 30.September(2010).Add(14.Hours().And(53.Minutes())).AsUtc().ToInstant()
            };

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<CreateBloodPressureInfoForPatientIdCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateBloodPressureInfoForPatientIdCommand cmd, CancellationToken _) =>
                    new BloodPressureInfo
                    {
                        DateOfMeasure = cmd.Data.DateOfMeasure,
                        Id = BloodPressureId.New(),
                        DiastolicPressure = cmd.Data.DiastolicPressure,
                        PatientId = cmd.Data.PatientId,
                        SystolicPressure = cmd.Data.SystolicPressure,
                        UpdatedDate = 23.June(2010).AsUtc().ToInstant()
                    }.Some<BloodPressureInfo, CreateCommandResult>())
                .Verifiable();
            PatientId patientId = PatientId.New();
            // Act

            IActionResult actionResult = await _controller.PostBloodPressure(patientId, newMeasure)
                .ConfigureAwait(false);

            // Assert
            _mediatorMock.Verify();

            Browsable<BloodPressureInfo> browsableResource = actionResult.Should()
                .BeOfType<CreatedAtRouteResult>().Which
                .Value.Should()
                    .BeAssignableTo<Browsable<BloodPressureInfo>>().Which;

            BloodPressureInfo resource = browsableResource.Resource;
            resource.Id.Should()
                .NotBe(BloodPressureId.Empty).And
                .NotBeNull();
            resource.DateOfMeasure.Should()
                .Be(newMeasure.DateOfMeasure);
            resource.DiastolicPressure.Should()
                .Be(newMeasure.DiastolicPressure);
            resource.PatientId.Should()
                .Be(patientId);

            IEnumerable<Link> links = browsableResource.Links;
            links.Should()
                .NotBeEmpty().And
                .NotContainNulls().And
                .NotContain(x => string.IsNullOrWhiteSpace(x.Href), $"{nameof(Link.Href)} must be provided for each link of the resource").And
                .NotContain(x => string.IsNullOrWhiteSpace(x.Relation), $"{nameof(Link.Relation)} must be provided for each link of the resource").And
                .Contain(x => x.Relation == "delete-bloodpressure").And
                .Contain(x => x.Relation == Self).And
                .Contain(x => x.Relation == "patient");

            Link linkToPatient = links.Single(x => x.Relation == "patient");
            linkToPatient.Href.Should()
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultGetOneByIdApi}/?controller={PatientsController.EndpointName}&id={resource.PatientId.Value}&version={_apiVersion}");

            Link linkToSelf = links.Single(x => x.Relation == Self);
            linkToSelf.Href.Should()
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultGetOneByIdApi}/?controller={BloodPressuresController.EndpointName}&id={resource.Id.Value}&version={_apiVersion}");

            Link linkToDelete = links.Single(x => x.Relation == "delete-bloodpressure");
            linkToSelf.Href.Should()
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultGetOneByIdApi}/?controller={BloodPressuresController.EndpointName}&id={resource.Id.Value}&version={_apiVersion}");
        }

        public static IEnumerable<object[]> MediatorReturnsErrorCases
        {
            get
            {
                yield return new object[]
                {
                    CreateCommandResult.Failed_NotFound,
                    (Expression<Func<IActionResult, bool>>)(actionResult => actionResult is NotFoundResult)
                };

                yield return new object[]
                {
                    CreateCommandResult.Failed_Conflict,
                    (Expression<Func<IActionResult, bool>>)(actionResult => actionResult is StatusCodeResult
                        && ((StatusCodeResult)actionResult).StatusCode == Status409Conflict)
                };
            }
        }

        [Theory]
        [MemberData(nameof(MediatorReturnsErrorCases))]
        public async Task GivenMediatorReturnsError_Controller_ReturnsAssociatedResponse(CreateCommandResult mediatorResult, Expression<Func<IActionResult, bool>> actionResultExpectation)
        {
            // Arrange
            _mediatorMock.Setup(mock => mock.Send(It.IsAny<CreateBloodPressureInfoForPatientIdCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Option.None<BloodPressureInfo, CreateCommandResult>(mediatorResult));

            // Act
            IActionResult actionResult = await _controller.PostBloodPressure(PatientId.New(), new NewBloodPressureModel())
                .ConfigureAwait(false);

            // Assert
            actionResult.Should()
                .NotBeNull().And
                .Match(actionResultExpectation);
        }

        [Fact]
        public async Task GivenModel_Post_Create_PatientResource()
        {
            // Arrange
            NewPatientInfo newPatient = new()
            {
                Name = "Solomon Grundy"
            };

            MeasuresApiOptions apiOptions = new() { DefaultPageSize = 25, MaxPageSize = 10 };
            _apiOptionsMock.Setup(mock => mock.Value).Returns(apiOptions);
            _mediatorMock.Setup(mock => mock.Send(It.IsAny<CreatePatientInfoCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreatePatientInfoCommand cmd, CancellationToken _) => new PatientInfo
                {
                    Name = cmd.Data.Name,
                    BirthDate = cmd.Data.BirthDate,
                    Id = PatientId.New()
                });

            // Act
            IActionResult actionResult = await _controller.Post(newPatient, ct: default)
                .ConfigureAwait(false);

            // Assert
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<CreatePatientInfoCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _apiOptionsMock.Verify(mock => mock.Value, Times.Once);
            CreatedAtRouteResult createdAtRouteResult = actionResult.Should()
                .BeAssignableTo<CreatedAtRouteResult>().Which;

            Browsable<PatientInfo> browsablePatientInfo = createdAtRouteResult.Value.Should()
                                                                                    .BeAssignableTo<Browsable<PatientInfo>>().Which;

            PatientInfo resource = browsablePatientInfo.Resource;
            resource.Should()
                .NotBeNull();

            createdAtRouteResult.RouteName.Should()
                .Be(RouteNames.DefaultGetOneByIdApi);
            createdAtRouteResult.RouteValues.Should()
                                            .Contain("id", resource.Id, "resource id must be provided in routeValues");


            IEnumerable<Link> links = browsablePatientInfo.Links;
            links.Should().NotBeNullOrEmpty().And
                          .NotContainNulls().And
                          .NotContain(link => string.IsNullOrWhiteSpace(link.Href), $"each resource link must provide its {nameof(Link.Href)}").And
                          .NotContain(link => string.IsNullOrWhiteSpace(link.Method), $"each resource link must provide its {nameof(Link.Method)}").And
                          .NotContain(link => string.IsNullOrWhiteSpace(link.Relation), $"each resource link must provide its {nameof(Link.Relation)}").And
                          .Contain(link => link.Relation == Self).And
                          .Contain(link => link.Relation == "bloodpressures");

            Link linkToSelf = links.Single(link => link.Relation == Self);
            linkToSelf.Href.Should()
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultGetOneByIdApi}/?controller={PatientsController.EndpointName}&id={resource.Id.Value}&version={_apiVersion}");
            linkToSelf.Method.Should()
                .Be("GET");

            Link linkToBloodPressures = links.Single(link => link.Relation == "bloodpressures");
            linkToBloodPressures.Href.Should()
                .BeEquivalentTo($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?controller={BloodPressuresController.EndpointName}&page=1&pageSize={apiOptions.DefaultPageSize}&patientId={resource.Id.Value}&version={_apiVersion}");
            linkToSelf.Method.Should()
                .Be("GET");
        }

        [Fact]
        public async Task Patch_UnknownEntity_Returns_NotFound()
        {
            JsonPatchDocument<PatientInfo> changes = new();
            changes.Replace(x => x.Name, string.Empty);

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<PatchCommand<Guid, PatientInfo>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModifyCommandResult.Failed_NotFound);

            // Act
            IActionResult actionResult = await _controller.Patch(Guid.NewGuid(), changes)
                .ConfigureAwait(false);

            // Assert
            actionResult.Should()
                .BeAssignableTo<NotFoundResult>();
        }

        [Fact]
        public async Task Patch_Valid_Resource_Returns_NoContentResult()
        {
            // Arrange
            JsonPatchDocument<PatientInfo> changes = new();
            changes.Replace(x => x.Name, string.Empty);

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<PatchCommand<Guid, PatientInfo>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModifyCommandResult.Done);

            // Act
            IActionResult actionResult = await _controller.Patch(Guid.NewGuid(), changes)
                .ConfigureAwait(false);

            // Assert
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<PatchCommand<Guid, PatientInfo>>(), It.IsAny<CancellationToken>()), Times.Once);

            actionResult.Should()
                .BeAssignableTo<NoContentResult>();
        }
    }
}