﻿namespace Agenda.CQRS.Features.Participants.Queries
{
    using Agenda.DTO;
    using Agenda.Ids;

    using MedEasy.CQRS.Core.Queries;

    using Optional;

    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Query to retrieve a planning for the specified participant between two date.
    /// </summary>
    public class GetPlanningByAttendeeIdQuery : QueryBase<Guid, (AttendeeId attendeeId, DateTimeOffset start, DateTimeOffset end), Option<IEnumerable<AppointmentInfo>>>
    {
        /// <summary>
        /// Builds a new <see cref="GetPlanningByAttendeeIdQuery"/> instance
        /// </summary>
        /// <param name="attendeeId">Id of the participant.</param>
        /// <param name="start">Start of the interval</param>
        /// <param name="end">End of the interval (inclusive).</param>
        /// <param name="page">index of the page of result</param>
        /// <param name="pageSize">number of items per page.</param
        public GetPlanningByAttendeeIdQuery(AttendeeId attendeeId, DateTimeOffset from, DateTimeOffset to)
            : base(Guid.NewGuid(), (attendeeId, from, to))
        {
            if (attendeeId == default)
            {
                throw new ArgumentOutOfRangeException(nameof(attendeeId), attendeeId, $"{nameof(attendeeId)} cannot be empty");
            }

            if (from > to)
            {
                throw new ArgumentException("The specified interval is not a valid period");
            }
        }
    }
}
