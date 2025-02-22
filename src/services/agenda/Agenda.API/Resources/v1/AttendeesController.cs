﻿namespace Agenda.API.Resources.v1
{
    using Agenda.API.Routing;
    using Agenda.CQRS.Features.Participants.Queries;
    using Agenda.DTO;
    using Agenda.DTO.Resources.Search;
    using Agenda.Ids;
    using Agenda.Models.v1.Appointments;
    using Agenda.Models.v1.Attendees;

    using AutoMapper;

    using MedEasy.Attributes;
    using MedEasy.DAL.Repositories;
    using MedEasy.RestObjects;

    using MediatR;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Options;

    using Optional;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using static MedEasy.RestObjects.LinkRelation;
    using static Microsoft.AspNetCore.Http.StatusCodes;

    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/[controller]")]
    [ApiController]
    [ProducesResponseType(Status400BadRequest)]
    public class AttendeesController
    {
        private readonly LinkGenerator _urlHelper;
        private readonly IMediator _mediator;
        private readonly IOptionsSnapshot<AgendaApiOptions> _apiOptions;
        private readonly IMapper _mapper;
        private readonly ApiVersion _apiVersion;

        /// <summary>
        /// Name of the endpoint
        /// </summary>²
        public static string EndpointName => nameof(AttendeesController).Replace("Controller", string.Empty);

        public AttendeesController(LinkGenerator urlHelper, IMediator mediator, IOptionsSnapshot<AgendaApiOptions> apiOptions, IMapper mapper, ApiVersion apiVersion)
        {
            _urlHelper = urlHelper;
            _mediator = mediator;
            _apiOptions = apiOptions;
            _mapper = mapper;
            _apiVersion = apiVersion;
        }

        /// <summary>
        /// Gets a page of participants resources. 
        /// The number of resources the page contains may be less than <paramref name="pageSize"/>.
        /// </summary>
        /// <param name="page">index of the page of results (1 for the first page, 2 for the next, ...)</param>
        /// <param name="pageSize">Number of resources per page</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpHead]
        [HttpGet]
        [ProducesResponseType(typeof(GenericPagedGetResponse<Browsable<AttendeeModel>>), Status200OK)]
        public async Task<ActionResult<GenericPagedGetResponse<Browsable<AttendeeModel>>>> Get([Minimum(1)] int page, [Minimum(1)] int pageSize, CancellationToken ct = default)
        {
            pageSize = Math.Min(pageSize, _apiOptions.Value.MaxPageSize);
            Page<AttendeeInfo> result = await _mediator.Send(new GetPageOfAttendeeInfoQuery(page, pageSize), ct)
                .ConfigureAwait(false);
            string version = _apiVersion.ToString();
            return new GenericPagedGetResponse<Browsable<AttendeeModel>>(

                _mapper.Map<IEnumerable<AttendeeModel>>(result.Entries).Select(x => new Browsable<AttendeeModel>
                {
                    Resource = x,
                    Links = new[]
                    {
                        new Link
                        {
                            Relation = Self,
                            Method = "GET",
                            Href = _urlHelper.GetPathByName(RouteNames.DefaultGetOneByIdApi, new { controller = EndpointName, x.Id, version })
                        }
                    }
                }),
                first: _urlHelper.GetPathByName(RouteNames.DefaultGetAllApi, new { controller = EndpointName, page = 1, pageSize, version }),
                previous: result.Count > 2 && page > 1
                    ? _urlHelper.GetPathByName(RouteNames.DefaultGetAllApi, new { controller = EndpointName, page = page - 1, pageSize, version })
                    : null,
                next: page < result.Count
                    ? _urlHelper.GetPathByName(RouteNames.DefaultGetAllApi, new { controller = EndpointName, page = page + 1, pageSize, version })
                    : null,

                last: _urlHelper.GetPathByName(RouteNames.DefaultGetAllApi, new { controller = EndpointName, page = result.Count, pageSize, version }),
                total: result.Total
            );
        }

        /// <summary>
        /// Gets a participant resouurce by its <paramref name="id"/>
        /// </summary>
        /// <param name="id">id of the resource to look for</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <response code="404"></response>

        [HttpGet("{id}")]
        [HttpHead("{id}")]
        public async Task<IActionResult> Get([RequireNonDefault] AttendeeId id, CancellationToken ct = default)
        {
            Option<AttendeeInfo> optionalResource = await _mediator.Send(new GetOneAttendeeInfoByIdQuery(id), ct)
                .ConfigureAwait(false);

            return optionalResource.Match<IActionResult>(
                some: resource =>
                {
                    string version = _apiVersion.ToString();
                    Browsable<AttendeeInfo> browsableResource = new()
                    {
                        Resource = resource,
                        Links = new[]
                        {
                            new Link
                            {
                                Relation = Self,
                                Method = "GET",
                                Href = _urlHelper.GetPathByName(RouteNames.DefaultGetOneByIdApi, new { controller = EndpointName, resource.Id, version })
                            },
                        }
                    };

                    return new OkObjectResult(resource);
                },

                none: () => new NotFoundResult());
        }

        /// <summary>
        /// Gets a list of appointments for the participant with the specified id
        /// </summary>
        /// <param name="id">id of the participant</param>
        /// <param name="from">start date interval</param>
        /// <param name="to">end date interval</param>
        /// <param name="ct"></param>
        /// 
        /// 
        /// <returns>Paginated list of appointments</returns>
        /// <reponse code="400">
        ///     either <paramref name="id"/> is not set,
        ///     specified date interval is not valid (<paramref name="from"/> is greater than <paramref name="to"/>
        /// </reponse>
        /// <reponse code="404">no participant with the specified <paramref name="id"/></reponse>
        [HttpHead("{id}/planning")]
        [HttpGet("{id}/planning")]
        [ProducesResponseType(Status404NotFound)]
        public async Task<ActionResult<IEnumerable<Browsable<AppointmentModel>>>> Planning([RequireNonDefault] AttendeeId id, DateTimeOffset from, DateTimeOffset to = default, CancellationToken ct = default)
        {
            IRequest<Option<IEnumerable<AppointmentInfo>>> query = new GetPlanningByAttendeeIdQuery(id, from, to);

            Option<IEnumerable<AppointmentInfo>> optionalAppointments = await _mediator.Send(query, ct)
                .ConfigureAwait(false);

            return optionalAppointments.Match(
                some: appointments =>
                {
                    string version = _apiVersion.ToString();
                    return new ActionResult<IEnumerable<Browsable<AppointmentModel>>>(
                        _mapper.Map<IEnumerable<AppointmentModel>>(appointments).Select(resource => new Browsable<AppointmentModel>
                        {
                            Resource = resource,
                            Links = new[]
                            {
                                new Link
                                {
                                    Relation = Self,
                                    Href =
                                    _urlHelper.GetPathByName(RouteNames.DefaultGetOneByIdApi, new { controller = AppointmentsController.EndpointName, resource.Id, version })
                                }
                            }
                        }));
                },
                none: () => new NotFoundResult()
            );
        }

        /// <summary>
        /// Search `participants` resources based on some criteria.
        /// </summary>
        /// <param name="search">Search criteria</param>
        /// <param name="ct">Notfies to cancel the search operation</param>
        /// <remarks>
        /// <para>
        ///     All criteria are combined as a AND.
        /// </para>
        /// <para>
        /// Advanded search :
        /// Several operators that can be used to make an advanced search :
        /// '*' : match zero or more characters in a string property.
        /// </para>
        /// <para>
        ///     // GET /participants/search?name=Wayne
        ///     will match all resources which have exactly 'Wayne' in the `name` property
        /// </para>
        /// <para>
        ///     // GET /participants/search?name=C*tral
        ///     will match all resources where name starts with 'C' and ends with 'tral'.
        /// </para>
        /// <para>'?' : match exactly one charcter in a string property.</para>
        /// <para>'!' : negate a criterion</para>
        /// <para>
        ///     // GET /participants/search?name=!Wayne
        ///     will match all resources where `name` is not "Wayne"
        /// </para>
        ///     
        /// </remarks>
        /// <response code="200">"page" of resources that matches <paramref name="search"/> criteria.</response>
        /// <response code="206">"page" of resources that matches <paramref name="search"/> criteria and there are more than one page of result.</response>
        /// <response code="404">"page" is greater than the number of pages in the result set.</response>
        /// <response code="400">one of the search criterion is not valid</response>
        [HttpGet("[action]")]
        [HttpHead("[action]")]
        [ProducesResponseType(typeof(PageLinks), Status404NotFound)]
        [ProducesResponseType(typeof(GenericPagedGetResponse<Browsable<AttendeeModel>>), Status206PartialContent)]
        public async Task<ActionResult<GenericPagedGetResponse<Browsable<AttendeeModel>>>> Search([FromQuery] SearchAttendeeModel search, CancellationToken ct = default)
        {
            search.PageSize = Math.Min(_apiOptions.Value.MaxPageSize, search.PageSize);

            SearchAttendeeInfo data = _mapper.Map<SearchAttendeeInfo>(search);

            string version = _apiVersion.ToString();

            Page<AttendeeInfo> page = await _mediator.Send(new SearchAttendeeInfoQuery(data), ct)
                                                     .ConfigureAwait(false);
            ActionResult<GenericPagedGetResponse<Browsable<AttendeeModel>>> actionResult;
            string linkToFirstPage = _urlHelper.GetPathByName(RouteNames.DefaultSearchResourcesApi, new { controller = EndpointName, search.Name, search.Email, search.Sort, page = 1, search.PageSize, version });
            string linkToLastPage = _urlHelper.GetPathByName(RouteNames.DefaultSearchResourcesApi, new { controller = EndpointName, search.Name, search.Email, search.Sort, page = page.Count, search.PageSize, version });

            if (search.Page > page.Count)
            {
                PageLinks validLinks = new(
                    first: linkToFirstPage,
                    previous: null,
                    next: null,
                    last: linkToLastPage);
                actionResult = new NotFoundObjectResult(validLinks);
            }
            else
            {
                actionResult = new GenericPagedGetResponse<Browsable<AttendeeModel>>(
                    _mapper.Map<IEnumerable<AttendeeModel>>(page.Entries).Select(x => new Browsable<AttendeeModel>
                    {
                        Resource = x,
                        Links = new[]
                        {
                            new Link
                            {
                                Relation = Self,
                                Method = "GET",
                                Href =_urlHelper.GetPathByName(RouteNames.DefaultGetOneByIdApi, new { x.Id, version })
                            }
                        }
                    }),
                    first: linkToFirstPage,
                    previous: page.Count > 1 && search.Page > 1
                        ? _urlHelper.GetPathByName(RouteNames.DefaultSearchResourcesApi, new { controller = EndpointName, search.Name, search.Email, search.Sort, page = search.Page - 1, search.PageSize, version })
                        : null,
                    next: search.Page < page.Count
                        ? _urlHelper.GetPathByName(RouteNames.DefaultSearchResourcesApi, new { controller = EndpointName, search.Name, search.Email, search.Sort, page = search.Page + 1, search.PageSize, version })
                        : null,
                    last: linkToLastPage,
                    total: page.Total
                );
            }

            return actionResult;
        }
    }
}
