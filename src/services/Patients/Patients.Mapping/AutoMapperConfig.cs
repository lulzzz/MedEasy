﻿using AutoMapper;
using MedEasy.Objects;
using MedEasy.RestObjects;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Patients.DTO;
using Patients.Objects;
using System;

namespace Patients.Mapping
{
    /// <summary>
    /// Contains mappings configuration
    /// </summary>
    public static class AutoMapperConfig
    {
        /// <summary>
        /// Creates a new <see cref="MapperConfiguration"/>
        /// </summary>
        /// <returns></returns>
        public static MapperConfiguration Build() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<IEntity<int>, Resource<Guid>>()
                .ForMember(dto => dto.Id, opt => opt.MapFrom(entity => entity.UUID))
                .ForMember(dto => dto.CreatedDate, opt => opt.Ignore())
                .ForMember(dto => dto.UpdatedDate, opt => opt.Ignore())
                .ReverseMap();

            cfg.CreateMap<CreatePatientInfo, Patient>()
                .ForMember(entity => entity.UUID, opt => opt.MapFrom(dto => dto.Id))
                .ForMember(entity => entity.Id, opt => opt.Ignore())
                .ForMember(entity => entity.CreatedBy, opt => opt.Ignore())
                .ForMember(entity => entity.UpdatedBy, opt => opt.Ignore())
                .IncludeBase<Resource<Guid>, IEntity<int>>()
                .ReverseMap();

            cfg.CreateMap<Patient, PatientInfo>()
                .ForMember(dto => dto.Id, opt => opt.MapFrom(entity => entity.UUID))
                .ForMember(dto => dto.MainDoctorId, opt => opt.Ignore())
                .IncludeBase<IEntity<int>, Resource<Guid>>()
                .ReverseMap()
                .ForMember(entity => entity.CreatedBy, opt => opt.Ignore())
                .ForMember(entity => entity.UpdatedBy, opt => opt.Ignore())
                .ForMember(entity => entity.UpdatedBy, opt => opt.Ignore())
                ;

            cfg.CreateMap(typeof(JsonPatchDocument<>), typeof(JsonPatchDocument<>));
            cfg.CreateMap(typeof(Operation<>), typeof(Operation<>));
        });
    }
}
