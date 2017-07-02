﻿using System;
using MedEasy.DAL.Interfaces;
using MedEasy.Validators;
using Microsoft.Extensions.Logging;
using MedEasy.Commands;
using MedEasy.Handlers.Core.Doctor.Commands;
using MedEasy.Handlers.Core.Commands;

namespace MedEasy.Handlers.Doctor.Commands
{
    /// <summary>
    /// Command runner for <see cref="PatchInfo{TResourceId}"/> commands
    /// </summary>
    public class RunPatchDoctorCommand : GenericPatchCommandRunner<Guid, Guid, int, Objects.Doctor, IPatchCommand<Guid, Objects.Doctor>>, IRunPatchDoctorCommand
    {
        /// <summary>
        /// Builds a new <see cref="RunPatchDoctorCommand"/> instance.
        /// </summary>
        /// <param name="uowFactory">Factory for building <see cref="IUnitOfWork"/> instances.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="validator">Validator for commands that will be run by <see cref="RunAsync(IPatchDoctorCommand)"/>.</param>
        public RunPatchDoctorCommand(IUnitOfWorkFactory uowFactory, ILogger<RunPatchDoctorCommand> logger, IValidate<IPatchCommand<Guid, Objects.Doctor>> validator)
            : base(validator, uowFactory)
        {
        }
    }
}
