﻿using AutoMapper;
using MedEasy.DTO;
using MedEasy.DTO.Autocomplete;
using MedEasy.Objects;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;

namespace MedEasy.Mapping
{
    public class AutoMapperConfig
    {
        public static MapperConfiguration Build() => new MapperConfiguration(cfg =>
        {


            cfg.CreateMap<Patient, PatientInfo>().ReverseMap();
            cfg.CreateMap<CreatePatientInfo, Patient>();
            cfg.CreateMap<CreateTemperatureInfo, Temperature>();
            cfg.CreateMap<Temperature, TemperatureInfo>().ReverseMap();
            cfg.CreateMap<CreateBloodPressureInfo, BloodPressure>();
            cfg.CreateMap<BloodPressure, BloodPressureInfo>();
            cfg.CreateMap<BodyWeight, BodyWeightInfo>();

            cfg.CreateMap<DocumentMetadata, DocumentMetadataInfo>().ReverseMap();
            cfg.CreateMap<Doctor, DoctorInfo>();

            cfg.CreateMap<Specialty, SpecialtyInfo>().ReverseMap();
            cfg.CreateMap<CreateSpecialtyInfo, Specialty>();

            cfg.CreateMap<Prescription, PrescriptionHeaderInfo>();
            cfg.CreateMap<CreatePrescriptionInfo, Prescription>();
            cfg.CreateMap<Prescription, PrescriptionInfo>().ReverseMap();
            cfg.CreateMap<PrescriptionItem, PrescriptionItemInfo>().ReverseMap();


            cfg.CreateMap<Appointment, AppointmentInfo>().ReverseMap();

            #region Autocomplete

            cfg.CreateMap<Doctor, DoctorAutocompleteInfo>()
                .ForMember(dest => dest.Value, opt => opt.MapFrom(source => source.Id))
                .ForMember(dest => dest.Text, opt => opt.MapFrom(source => source.Firstname + " " + source.Lastname))
                .ForMember(dest => dest.Specialty, opt => opt.MapFrom(source => source.Specialty.Name));


            #endregion

            cfg.CreateMap(typeof(JsonPatchDocument<>), typeof(JsonPatchDocument<>));
            cfg.CreateMap(typeof(Operation<>), typeof(Operation<>));


        });
    }
}
