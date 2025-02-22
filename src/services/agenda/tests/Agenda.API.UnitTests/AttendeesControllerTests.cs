﻿namespace Agenda.API.UnitTests.Resources.v1
{
    using Agenda.API.Resources.v1;
    using Agenda.API.Routing;
    using Agenda.CQRS.Features.Participants.Queries;
    using Agenda.DataStores;
    using Agenda.DTO;
    using Agenda.DTO.Resources.Search;
    using Agenda.Ids;
    using Agenda.Mapping;
    using Agenda.Models.v1.Appointments;
    using Agenda.Models.v1.Attendees;
    using Agenda.Objects;

    using AutoMapper;
    using AutoMapper.QueryableExtensions;

    using Bogus;

    using DataFilters;

    using FakeItEasy;

    using FluentAssertions;
    using FluentAssertions.Extensions;

    using MedEasy.DAL.EFStore;
    using MedEasy.DAL.Interfaces;
    using MedEasy.DAL.Repositories;
    using MedEasy.IntegrationTests.Core;
    using MedEasy.RestObjects;

    using MediatR;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;

    using Moq;

    using NodaTime;
    using NodaTime.Extensions;
    using NodaTime.Testing;

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

    using static MedEasy.RestObjects.LinkRelation;
    using static Moq.MockBehavior;
    using static System.StringComparison;

    [UnitTest]
    [Feature("Agenda")]
    public class AttendeesControllerTests : IAsyncLifetime, IClassFixture<SqliteEfCoreDatabaseFixture<AgendaContext>>
    {
        private ITestOutputHelper _outputHelper;
        private Mock<IMapper> _mapperMock;
        private Mock<LinkGenerator> _urlHelperMock;
        private IUnitOfWorkFactory _uowFactory;
        private Mock<IOptionsSnapshot<AgendaApiOptions>> _apiOptionsMock;
        private Mock<IMediator> _mediatorMock;
        private AttendeesController _sut;
        private const string _baseUrl = "agenda";
        private static readonly ApiVersion ApiVersion = new(1, 0);

        public AttendeesControllerTests(ITestOutputHelper outputHelper, SqliteEfCoreDatabaseFixture<AgendaContext> database)
        {
            _urlHelperMock = new Mock<LinkGenerator>(Strict);
            _urlHelperMock.Setup(mock => mock.GetPathByAddress(It.IsAny<string>(), It.IsAny<RouteValueDictionary>(), It.IsAny<PathString>(), It.IsAny<FragmentString>(), It.IsAny<LinkOptions>()))
                .Returns((string routename, RouteValueDictionary routeValues, PathString _, FragmentString __, LinkOptions ___)
                => $"{_baseUrl}/{routename}/?{routeValues?.ToQueryString()}");

            _uowFactory = new EFUnitOfWorkFactory<AgendaContext>(database.OptionsBuilder.Options, (options) =>
            {
                AgendaContext dbContext = new(options, new FakeClock(new Instant()));
                dbContext.Database.EnsureCreated();
                return dbContext;
            });

            _apiOptionsMock = new Mock<IOptionsSnapshot<AgendaApiOptions>>(Strict);

            _mediatorMock = new Mock<IMediator>(Strict);
            _outputHelper = outputHelper;

            _mapperMock = new Mock<IMapper>(Strict);

            IMapper mapper = AutoMapperConfig.Build().CreateMapper();
            _mapperMock.Setup(mock => mock.Map<IEnumerable<AttendeeModel>>(It.IsAny<IEnumerable<AttendeeInfo>>()))
                .Returns((IEnumerable<AttendeeInfo> input) => mapper.Map<IEnumerable<AttendeeModel>>(input));
            _mapperMock.Setup(mock => mock.Map<IEnumerable<AppointmentModel>>(It.IsAny<IEnumerable<AppointmentInfo>>()))
                .Returns((IEnumerable<AppointmentInfo> input) => mapper.Map<IEnumerable<AppointmentModel>>(input));
            _mapperMock.Setup(mock => mock.Map<AttendeeModel>(It.IsAny<AttendeeInfo>()))
                .Returns((AttendeeInfo input) => mapper.Map<AttendeeModel>(input));
            _mapperMock.Setup(mock => mock.Map<SearchAttendeeInfo>(It.IsAny<SearchAttendeeModel>()))
                .Returns((SearchAttendeeModel input) => mapper.Map<SearchAttendeeInfo>(input));


            _sut = new AttendeesController(urlHelper: _urlHelperMock.Object, mediator: _mediatorMock.Object, apiOptions: _apiOptionsMock.Object,
                    _mapperMock.Object, ApiVersion
                );
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Attendee>().Clear();

                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
            _uowFactory = null;
            _outputHelper = null;
            _urlHelperMock = null;
            _apiOptionsMock = null;
            _mediatorMock = null;
            _mapperMock = null;
            _sut = null;
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
                            Enumerable.Empty<Attendee>(), // Current store state
                            (pageSize, page), // request,
                            (defaultPageSize : 30, maxPageSize : 200),
                            0,    //expected total
                            (
                                first : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == First
                                    && ($"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?" +
                                    $"Controller={AttendeesController.EndpointName}" +
                                    "&page=1" +
                                    $"&pageSize={(pageSize < 1 ? 1 : Math.Min(pageSize, 200))}&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                                previous : (Expression<Func<Link, bool>>) (x => x == null), // expected link to previous page
                                next :(Expression<Func<Link, bool>>) (x => x == null), // expected link to next page
                                last : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Last
                                    && ($"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?" +
                                    $"Controller={AttendeesController.EndpointName}" +
                                    "&page=1" +
                                    $"&pageSize={(pageSize < 1 ? 1 : Math.Min(pageSize, 200))}&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase))
                            )  // expected link to last page
                        };
                    }
                }

                Faker<Attendee> attendeeFaker = new Faker<Attendee>()
                    .CustomInstantiator(faker => new Attendee(AttendeeId.New(), faker.Person.FullName));

                IEnumerable<Attendee> items = attendeeFaker.Generate(20);

                yield return new object[]
                {
                    items,
                    (pageSize : 5, page : 1), // request
                    (defaultPageSize : 30, maxPageSize : 200),
                    20,    //expected total
                    (
                        first : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == First && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=1&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        previous : (Expression<Func<Link, bool>>) (x => x == null), // expected link to previous page
                        next : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Next && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=2&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to next page
                        last : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Last && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=4&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase))
                    )  // expected link to last page
                };

                yield return new object[]
                {
                    items,
                    (pageSize : 5, page : 4), // request
                    (defaultPageSize : 30, maxPageSize : 200),
                    20,    //expected total
                    (
                        first : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == First && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=1&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        previous : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Previous && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=3&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        next : (Expression<Func<Link, bool>>) (x => x == null ), // expected link to next page
                        last : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Last && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=4&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase))
                    )  // expected link to last page
                };

                yield return new object[]
               {
                    items,
                    (pageSize : 5, page : 2), // request
                    (defaultPageSize : 30, maxPageSize : 200),
                    20,    //expected total
                    (
                        first : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == First && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=1&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        previous : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Previous && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=1&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        next : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Next && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=3&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        last : (Expression<Func<Link, bool>>) (x => x != null && x.Relation == Last && $"{_baseUrl}/{RouteNames.DefaultGetAllApi}/?Controller={AttendeesController.EndpointName}&page=4&pageSize=5&version={ApiVersion}".Equals(x.Href, OrdinalIgnoreCase))
                    )  // expected link to last page
               };
            }
        }

        [Theory]
        [MemberData(nameof(GetAllTestCases))]
        public async Task GetAll(IEnumerable<Attendee> items, (int pageSize, int page) request,
            (int defaultPageSize, int maxPageSize) pagingOptions,
            int expectedCount,
            (Expression<Func<Link, bool>> firstPageUrlExpectation, Expression<Func<Link, bool>> previousPageUrlExpectation, Expression<Func<Link, bool>> nextPageUrlExpectation, Expression<Func<Link, bool>> lastPageUrlExpectation) linksExpectation)
        {
            _outputHelper.WriteLine($"Testing {nameof(AttendeesController.Get)}({nameof(PaginationConfiguration)})");
            _outputHelper.WriteLine($"{nameof(request)}{nameof(request.pageSize)}: {request.pageSize}");
            _outputHelper.WriteLine($"{nameof(request)}{nameof(request.page)}: {request.page}");
            _outputHelper.WriteLine($"items count: {items.Count()}");

            // Arrange
            using (IUnitOfWork uow = _uowFactory.NewUnitOfWork())
            {
                uow.Repository<Attendee>().Create(items);
                await uow.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<GetPageOfAttendeeInfoQuery>(), It.IsAny<CancellationToken>()))
                .Returns((GetPageOfAttendeeInfoQuery query, CancellationToken ct) =>
                {
                    using IUnitOfWork uow = _uowFactory.NewUnitOfWork();
                    Expression<Func<Attendee, AttendeeInfo>> selector = AutoMapperConfig.Build().ExpressionBuilder.GetMapExpression<Attendee, AttendeeInfo>();
                    return uow.Repository<Attendee>()
                              .ReadPageAsync(
                                    selector,
                                    query.Data.PageSize,
                                    query.Data.Page,
                                    new Sort<Attendee>(nameof(Attendee.UpdatedDate)),
                                    ct)
                              .AsTask();
                });

            _apiOptionsMock.SetupGet(mock => mock.Value).Returns(new AgendaApiOptions { DefaultPageSize = pagingOptions.defaultPageSize, MaxPageSize = pagingOptions.maxPageSize });

            // Act
            ActionResult<GenericPagedGetResponse<Browsable<AttendeeModel>>> actionResult = await _sut.Get(page: request.page, pageSize: request.pageSize, ct: default)
                                                                                                     .ConfigureAwait(false);

            // Assert
            _apiOptionsMock.VerifyGet(mock => mock.Value, Moq.Times.Once, $"{nameof(AttendeesController)}.{nameof(AttendeesController.Get)} must always check that {nameof(PaginationConfiguration.PageSize)} don't exceed {nameof(AgendaApiOptions.MaxPageSize)} value");
            _mediatorMock.Verify(mock => mock.Send(It.IsAny<GetPageOfAttendeeInfoQuery>(), It.IsAny<CancellationToken>()), Moq.Times.Once);

            GenericPagedGetResponse<Browsable<AttendeeModel>> response = actionResult.Value;

            _outputHelper.WriteLine($"response : {response}");

            response.Items.Should()
                          .NotBeNull();

            if (response.Items.Any())
            {
                response.Items.Should()
                    .NotContainNulls().And
                    .OnlyContain(x => x.Resource != null, "resource must not be null").And
                    .OnlyContain(x => x.Links.Once(link => link.Relation == Self), "links must contain one self relation");
            }

            response.Total.Should()
                          .Be(expectedCount, $@"the ""{nameof(GenericPagedGetResponse<AttendeeInfo>)}.{nameof(GenericPagedGetResponse<AttendeeInfo>.Total)}"" property indicates the number of elements");

            response.Links.First.Should().Match(linksExpectation.firstPageUrlExpectation);
            response.Links.Previous.Should().Match(linksExpectation.previousPageUrlExpectation);
            response.Links.Next.Should().Match(linksExpectation.nextPageUrlExpectation);
            response.Links.Last.Should().Match(linksExpectation.lastPageUrlExpectation);
        }

        [Fact]
        public async Task WhenMediatorReturnsNotFound_GetById_ReturnsNotFoundResult()
        {
            // Arrange
            AttendeeId attendeeId = AttendeeId.New();

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<IRequest<Option<AttendeeInfo>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Option.None<AttendeeInfo>());

            // Act
            IActionResult actionResult = await _sut.Get(id: attendeeId, ct: default)
                .ConfigureAwait(false);

            // Assert
            actionResult.Should()
                .BeAssignableTo<NotFoundResult>();

            _mediatorMock.Verify(mock => mock.Send(It.IsAny<IRequest<Option<AttendeeInfo>>>(), It.IsAny<CancellationToken>()), Moq.Times.Once);
            _mediatorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task WhenMediatorReturnsNotFound_GetAppointmentsByParticipantId_Returns_NotFound()
        {
            // Arrange
            AttendeeId attendeeId = AttendeeId.New();

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<IRequest<Option<IEnumerable<AppointmentInfo>>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Option.None<IEnumerable<AppointmentInfo>>());

            // Act
            ActionResult<IEnumerable<Browsable<AppointmentModel>>> actionResult = await _sut.Planning(id: attendeeId, from: 1.January(2019), to: 31.January(2019), ct: default)
                .ConfigureAwait(false);

            // Assert
            actionResult.Result.Should()
                .NotBeNull().And
                .BeOfType<NotFoundResult>();

            _mediatorMock.Verify(mock => mock.Send(It.IsAny<IRequest<Option<IEnumerable<AppointmentInfo>>>>(), It.IsAny<CancellationToken>()), Moq.Times.Once);
            _mediatorMock.Verify(mock => mock.Send(It.Is<GetPlanningByAttendeeIdQuery>(query =>
                query.Data.attendeeId == attendeeId
                && query.Data.start == 1.January(2019) && query.Data.end == 31.January(2019)), It.IsAny<CancellationToken>()), Moq.Times.Once);
            _mediatorMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task WhenMediatorReturnsData_GetAppointmentsByParticipantId_Returns_Results()
        {
            // Arrange
            AttendeeId attendeeId = AttendeeId.New();

            Faker<AppointmentInfo> appointmentInfo = new Faker<AppointmentInfo>()
                .RuleFor(x => x.Id, () => AppointmentId.New())
                .RuleFor(x => x.StartDate, () => 13.January(2010).Add(14.Hours()).AsUtc().ToInstant())
                .RuleFor(x => x.EndDate, (_, current) => current.StartDate + 1.Hours().ToDuration())
                .RuleFor(x => x.CreatedDate, (faker) => faker.Noda().Instant.Recent())
                .RuleFor(x => x.Attendees, _ => new[] {
                    new AttendeeInfo {Name = "Hugo strange", Id = attendeeId },
                    new AttendeeInfo {Name = "Joker", Id = AttendeeId.New()}
                })
                ;

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<IRequest<Option<IEnumerable<AppointmentInfo>>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Option.Some(appointmentInfo.GenerateLazy(count: 10)));

            // Act
            ActionResult<IEnumerable<Browsable<AppointmentModel>>> actionResult = await _sut.Planning(id: attendeeId,
                                                                                                      from: 1.January(2019),
                                                                                                      to: 31.January(2019),
                                                                                                      ct: default)
                                                                                            .ConfigureAwait(false);

            // Assert
            actionResult.Value.Should()
                              .NotBeNull().And
                              .BeAssignableTo<IEnumerable<Browsable<AppointmentModel>>>();

            IEnumerable<Browsable<AppointmentModel>> appointments = actionResult.Value;
            appointments.Should()
                        .NotBeNull().And
                        .NotContainNulls().And
                        .OnlyContain(browsable => browsable.Resource != default).And
                        .OnlyContain(browsable => browsable.Links.Once()).And
                        .OnlyContain(browsable => browsable.Links.Once(link => link.Relation == Self));

            _mediatorMock.Verify(mock => mock.Send(It.IsAny<IRequest<Option<IEnumerable<AppointmentInfo>>>>(), It.IsAny<CancellationToken>()), Moq.Times.Once);
            _mediatorMock.Verify(mock => mock.Send(It.Is<GetPlanningByAttendeeIdQuery>(query =>
                query.Data.attendeeId == attendeeId
                && query.Data.start == 1.January(2019) && query.Data.end == 31.January(2019)), It.IsAny<CancellationToken>()), Moq.Times.Once);
            _mediatorMock.VerifyNoOtherCalls();
        }

        public static IEnumerable<object[]> SearchCases
        {
            get
            {
                (int defaultPageSize, int maxPageSize) pagingOptions = (10, 200);
                {
                    SearchAttendeeModel searchInfo = new()
                    {
                        Name = "bruce",
                        Page = 1,
                        PageSize = 30,
                        Sort = "-Name"
                    };

                    yield return new object[]
                    {
                        Page<AttendeeInfo>.Empty(30),
                        searchInfo,
                        pagingOptions,
                        0,
                        (
                        (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == First
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"Controller={AttendeesController.EndpointName}" +
                                $"&name={searchInfo.Name}"+
                                "&page=1&pageSize=30" +
                                $"&sort={searchInfo.Sort}&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                        (Expression<Func<Link, bool>>)(previous => previous == null),
                        (Expression<Func<Link, bool>>)(next => next == null),
                        (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == Last
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"Controller={AttendeesController.EndpointName}" +
                                $"&name={searchInfo.Name}"+
                                "&page=1" +
                                "&pageSize=30" +
                                $"&sort={searchInfo.Sort}&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase)))
                    };
                }
                {
                    SearchAttendeeModel searchInfo = new()
                    {
                        Name = "!*Wayne",
                        Page = 1,
                        PageSize = 30,
                        Sort = "-Name"
                    };
                    yield return new object[]
                    {
                        new Page<AttendeeInfo> (
                            entries: new []
                            {
                                new AttendeeInfo { Name = "Bruce Wayne" }
                            },
                            total : 1,
                            size : searchInfo.PageSize),
                        searchInfo,
                        pagingOptions,
                        1,
                        (
                           (Expression<Func<Link, bool>>) (x => x != null
                            && x.Relation == First
                            && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                $"Controller={AttendeesController.EndpointName}" +
                                $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                "&page=1&pageSize=30" +
                                $"&sort={searchInfo.Sort}&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase)),
                            (Expression<Func<Link, bool>>)(previous => previous == null),
                            (Expression<Func<Link, bool>>)(next => next == null),
                            (Expression<Func<Link, bool>>) (x => x != null
                                && x.Relation == Last
                                && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                    $"Controller={AttendeesController.EndpointName}" +
                                    $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                    "&page=1&pageSize=30" +
                                    $"&sort={searchInfo.Sort}&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase))
                        )
                    };
                }
                {
                    SearchAttendeeModel searchInfo = new()
                    {
                        Name = "Bruce",
                        Page = 1,
                        PageSize = 30,
                    };
                    yield return new object[]
                    {
                        new Page<AttendeeInfo> (
                            entries: new []
                            {
                                new AttendeeInfo { Name = "Bruce Wayne" }
                            },
                            total : 1,
                            size : searchInfo.PageSize),
                        searchInfo,
                        pagingOptions,
                        1,
                        (
                            (Expression<Func<Link, bool>>) (x => x != null
                                && x.Relation == First
                                && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                    $"Controller={AttendeesController.EndpointName}" +
                                    $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                    $"&page=1&pageSize=30&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase)), // expected link to first page
                            (Expression<Func<Link, bool>>)(previous => previous == null),
                            (Expression<Func<Link, bool>>)(next => next == null),
                            (Expression<Func<Link, bool>>) (x => x != null
                                && x.Relation == Last
                                && ($"{_baseUrl}/{RouteNames.DefaultSearchResourcesApi}/?" +
                                    $"Controller={AttendeesController.EndpointName}" +
                                    $"&name={Uri.EscapeDataString(searchInfo.Name)}"+
                                    $"&page=1&pageSize=30&version={ApiVersion}").Equals(x.Href, OrdinalIgnoreCase))
                        )
                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(SearchCases))]
        public async Task Search(Page<AttendeeInfo> page, SearchAttendeeModel request,
            (int defaultPageSize, int maxPageSize) pagingOptions,
            int expectedCount,
            (Expression<Func<Link, bool>> firstPageUrlExpectation, Expression<Func<Link, bool>> previousPageUrlExpectation, Expression<Func<Link, bool>> nextPageUrlExpectation, Expression<Func<Link, bool>> lastPageUrlExpectation) linksExpectation)
        {
            _outputHelper.WriteLine($"Testing {nameof(AttendeesController)}({nameof(AttendeesController.Search)})");
            _outputHelper.WriteLine($"{nameof(request)} : {request.Jsonify()}");
            _outputHelper.WriteLine($"page of result returned by mediator : {page.Jsonify()}");

            // Arrange

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<SearchAttendeeInfoQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(page);

            _apiOptionsMock.SetupGet(mock => mock.Value)
                           .Returns(new AgendaApiOptions { DefaultPageSize = pagingOptions.defaultPageSize, MaxPageSize = pagingOptions.maxPageSize });

            // Act
            ActionResult<GenericPagedGetResponse<Browsable<AttendeeModel>>> actionResult = await _sut.Search(request, ct: default)
                                                                                                     .ConfigureAwait(false);

            // Assert
            _apiOptionsMock.VerifyGet(mock => mock.Value, Moq.Times.Once, $"{nameof(AttendeesController)}.{nameof(AttendeesController.Search)} must always check that {nameof(SearchAttendeeInfo.PageSize)} don't exceed {nameof(AgendaApiOptions.MaxPageSize)} value");
            _apiOptionsMock.VerifyNoOtherCalls();

            _mediatorMock.Verify(mock => mock.Send(It.IsAny<IRequest<Page<AttendeeInfo>>>(), It.IsAny<CancellationToken>()), Moq.Times.Once);
            _mediatorMock.VerifyNoOtherCalls();

            _mapperMock.Verify(mock => mock.Map<SearchAttendeeInfo>(It.IsAny<SearchAttendeeModel>()), Moq.Times.Once);
            _mapperMock.Verify(mock => mock.Map<SearchAttendeeInfo>(It.Is<SearchAttendeeModel>(input => input == request)), Moq.Times.Once);

            GenericPagedGetResponse<Browsable<AttendeeModel>> response = actionResult.Value;

            _outputHelper.WriteLine($"response : {response}");

            response.Items.Should()
                          .NotBeNull();

            if (response.Items.Any())
            {
                response.Items.Should()
                              .NotContainNulls().And
                              .OnlyContain(x => x.Resource != null, "resource must not be null").And
                              .OnlyContain(x => x.Links.Once(link => link.Relation == Self), "links must contain only self relation");
            }

            response.Total.Should()
                    .Be(expectedCount, $@"the ""{nameof(GenericPagedGetResponse<AttendeeInfo>)}.{nameof(GenericPagedGetResponse<AttendeeInfo>.Total)}"" property indicates the number of elements");

            response.Links.First.Should().Match(linksExpectation.firstPageUrlExpectation);
            response.Links.Previous.Should().Match(linksExpectation.previousPageUrlExpectation);
            response.Links.Next.Should().Match(linksExpectation.nextPageUrlExpectation);
            response.Links.Last.Should().Match(linksExpectation.lastPageUrlExpectation);
        }

        public static IEnumerable<object[]> SearchReturnsNotFoundCases
        {
            get
            {
                (int defaultPageSize, int maxPageSize) pagingOptions = (defaultPageSize: 10, maxPageSize: 300);
                yield return new object[]
                {
                    Page<AttendeeInfo>.Empty(pagingOptions.defaultPageSize),
                    pagingOptions,
                    new SearchAttendeeModel { Page = 2, Name = "Bruce*" },
                    (Expression<Func<PageLinks, bool>>)(pageLinks => pageLinks.First != null
                        && ($"agenda/{RouteNames.DefaultSearchResourcesApi}/?" +
                        $"controller={AttendeesController.EndpointName}" +
                        $"&name={Uri.EscapeDataString("Bruce*")}" +
                        $"&page=1&pageSize=30&version={ApiVersion}").Equals(pageLinks.First.Href, OrdinalIgnoreCase)
                    ),
                    "The result is empty so there's no page with index 2"
                };
            }
        }

        [Theory]
        [MemberData(nameof(SearchReturnsNotFoundCases))]
        public async Task Search_Returns_NotFound_When_PageIndex_Exceed_PageCount(Page<AttendeeInfo> page, (int defaultPageSize, int maxPageSize) pagingOptions, SearchAttendeeModel query, Expression<Func<PageLinks, bool>> resultExpectation, string reason)
        {
            // Arrange

            _mediatorMock.Setup(mock => mock.Send(It.IsAny<SearchAttendeeInfoQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(page);

            _apiOptionsMock.SetupGet(mock => mock.Value)
                .Returns(new AgendaApiOptions { DefaultPageSize = pagingOptions.defaultPageSize, MaxPageSize = pagingOptions.maxPageSize });

            // Act
            ActionResult<GenericPagedGetResponse<Browsable<AttendeeModel>>> actionResult = await _sut.Search(query, default)
                .ConfigureAwait(false);

            _mediatorMock.Verify(mock => mock.Send(It.IsAny<SearchAttendeeInfoQuery>(), It.IsAny<CancellationToken>()), Moq.Times.Once);
            _mediatorMock.VerifyNoOtherCalls();

            _mapperMock.Verify(mock => mock.Map<SearchAttendeeInfo>(It.IsAny<SearchAttendeeModel>()), Moq.Times.Once);
            _mapperMock.Verify(mock => mock.Map<SearchAttendeeInfo>(It.Is<SearchAttendeeModel>(input => input == query)), Moq.Times.Once);
            _mapperMock.Verify(mock => mock.Map<IEnumerable<AttendeeModel>>(It.IsAny<IEnumerable<AttendeeInfo>>()), Moq.Times.Never);
            _mapperMock.VerifyNoOtherCalls();

            // Assert
            actionResult.Result.Should()
                .BeAssignableTo<NotFoundObjectResult>().Which.Value.Should()
                .BeAssignableTo<PageLinks>().Which.Should()
                .Match(resultExpectation, reason);
        }
    }
}
