﻿using MedEasy.Ids;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using System;

namespace Patients.Ids
{
    public record PatientId(Guid Value) : StronglyTypedId<Guid>(Value)
    {
        public static PatientId Empty => new(Guid.Empty);
        public static PatientId New() => new(Guid.NewGuid());

        public class EfValueConverter : ValueConverter<PatientId, Guid>
        {
            public EfValueConverter(ConverterMappingHints mappingHints = null)
                : base(id => id.Value, value => new PatientId(value), mappingHints) { }
        }
    }
}
