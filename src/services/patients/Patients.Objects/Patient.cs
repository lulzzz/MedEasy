﻿namespace Patients.Objects
{
    using MedEasy.Objects;

    using NodaTime;

    using Patients.Ids;

    /// <summary>
    /// Patient entity
    /// </summary>
    public class Patient : AuditableEntity<PatientId, Patient>
    {
        /// <summary>
        /// Firstname
        /// </summary>
        public string Firstname { get; private set; }

        /// <summary>
        /// Lastname
        /// </summary>
        public string Lastname { get; private set; }

        /// <summary>
        /// When the patient was born
        /// </summary>
        public LocalDate? BirthDate { get; private set; }

        /// <summary>
        /// Where the patient was born
        /// </summary>
        public string BirthPlace { get; private set; }

        /// <summary>
        /// Builds a new <see cref="Patient"/> instance.
        /// </summary>
        /// <param name="id">Id of the instance. Should uniquely identity the instance</param>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <param name="birthplace"></param>
        /// <param name="birthDate"></param>
        public Patient(PatientId id, string firstname, string lastname)
            : base(id)
        {
            Firstname = firstname;
            Lastname = lastname;
        }

        /// <summary>
        /// Defines the birthdate
        /// </summary>
        /// <param name="birthDate"></param>
        /// <returns></returns>
        public Patient WasBornOn(LocalDate? birthDate)
        {
            BirthDate = birthDate;
            return this;
        }

        /// <summary>
        /// Defines where the patient was born
        /// </summary>
        /// <param name="birthPlace"></param>
        /// <returns></returns>
        public Patient WasBornIn(string birthPlace)
        {
            BirthPlace = birthPlace;
            return this;
        }

        /// <summary>
        /// Changes the <see cref="Patient"/>'s <see cref="Firstname"/>
        /// </summary>
        /// <param name="newFirstname"></param>
        /// <returns></returns>
        public Patient ChangeFirstnameTo(string newFirstname)
        {
            Firstname = newFirstname;
            return this;
        }

        /// <summary>
        /// Changes the <see cref="Patient"/>'s <see cref="Lastname"/>
        /// </summary>
        /// <param name="newLastname"></param>
        /// <returns></returns>
        public Patient ChangeLastnameTo(string newLastname)
        {
            Lastname = newLastname;
            return this;
        }
    }
}
