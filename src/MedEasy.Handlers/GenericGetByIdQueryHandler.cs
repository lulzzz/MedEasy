﻿using System.Threading.Tasks;
using MedEasy.Validators;
using Microsoft.Extensions.Logging;
using MedEasy.DAL.Interfaces;
using System.Collections.Generic;
using System.Linq;
using static MedEasy.Validators.ErrorLevel;
using MedEasy.Handlers.Exceptions;
using AutoMapper.QueryableExtensions;
using System;
using MedEasy.Objects;
using MedEasy.Queries;
using System.Linq.Expressions;

namespace MedEasy.Handlers.Queries
{

    /// <summary>
    /// Generic handler for queries that request one single resource.
    /// </summary>
    /// <typeparam name="TQueryId">Type of the identifier of queries</typeparam>
    /// <typeparam name="TEntity">Type of the entity to create</typeparam>
    /// <typeparam name="TEntityId">Type of the data of queries this handles will carry. This is also the type of the resource identifier</typeparam>
    /// <typeparam name="TResult">Type of the query execution résult</typeparam>
    /// <typeparam name="TQuery">Type of the query</typeparam>
    public abstract class GenericGetOneByIdQueryHandler<TQueryId, TEntity, TEntityId, TResult, TQuery> : GenericGetOneByIdQueryHandler<TQueryId, TEntity, TEntityId, TResult, TQuery, IValidate<TQuery>>
        where TQuery : IQuery<TQueryId, TEntityId, TResult>
        where TEntity : class, IEntity<TEntityId>
        where TQueryId : IEquatable<TQueryId>
    {

        /// <summary>
        /// Builds a new <see cref="GenericGetOneByIdQueryHandler{TKey, TEntity, TData, TResult, TCommand}"/> instance
        /// </summary>
        /// <param name="validator">Validator to use to validate commands in <see cref="HandleAsync(TQuery)"/></param>
        /// <param name="logger">Logger</param>
        /// <param name="uowFactory">Factory to build instances of <see cref="IUnitOfWork"/></param>
        /// <param name="dataToEntityMapper">Function to convert commands data to entity</param>
        /// <param name="expressionBuilder">Container of expressions that will be used to convert <see cref="TEntity"/> to <see cref="TResult"/></param>
        protected GenericGetOneByIdQueryHandler(
            ILogger<GenericGetOneByIdQueryHandler<TQueryId, TEntity, TEntityId, TResult, TQuery>> logger,
            IUnitOfWorkFactory uowFactory, IExpressionBuilder expressionBuilder) : base(Validator<TQuery>.Default, logger, uowFactory, expressionBuilder)
        {
        }
    }

    /// <summary>
    /// Generic handler for queries that request one single resource.
    /// </summary>
    /// <typeparam name="TQueryId">Type of the identifier of queries</typeparam>
    /// <typeparam name="TEntity">Type of the entity to create</typeparam>
    /// <typeparam name="TEntityId">Type of the data of queries this handles will carry. This is also the type of the resource identifier</typeparam>
    /// <typeparam name="TResult">Type of the query execution résult</typeparam>
    /// <typeparam name="TQuery">Type of the query</typeparam>
    /// <typeparam name="TQueryValidator">Type of the validator of the <typeparamref name="TQuery"/> query</typeparam>
        public abstract class GenericGetOneByIdQueryHandler<TQueryId, TEntity, TEntityId, TResult, TQuery, TQueryValidator> : QueryHandlerBase<TQueryId, TEntity, TEntityId, TResult, TQuery, TQueryValidator> 
        where TQuery : IQuery<TQueryId, TEntityId, TResult>
        where TEntity : class, IEntity<TEntityId>
        where TQueryId : IEquatable<TQueryId>
        where TQueryValidator : IValidate<TQuery>
    {

        /// <summary>
        /// Builds a new <see cref="GenericGetOneByIdQueryHandler{TKey, TEntity, TData, TResult, TCommand}"/> instance
        /// </summary>
        /// <param name="validator">Validator to use to validate commands in <see cref="HandleAsync(TQuery)"/></param>
        /// <param name="logger">Logger</param>
        /// <param name="uowFactory">Factory to build instances of <see cref="IUnitOfWork"/></param>
        /// <param name="dataToEntityMapper">Function to convert commands data to entity</param>
        /// <param name="expressionBuilder">Container of expressions that will be used to convert <see cref="TEntity"/> to <see cref="TResult"/></param>
        protected GenericGetOneByIdQueryHandler(TQueryValidator validator, 
            ILogger<GenericGetOneByIdQueryHandler<TQueryId, TEntity, TEntityId, TResult, TQuery, TQueryValidator>> logger, 
            IUnitOfWorkFactory uowFactory, IExpressionBuilder expressionBuilder
            ) : base (validator, uowFactory)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            if (expressionBuilder == null)
            {
                throw new ArgumentNullException(nameof(expressionBuilder));
            }
            Logger = logger;
            ExpressionBuilder = expressionBuilder;
        }

        
        protected ILogger<GenericGetOneByIdQueryHandler<TQueryId, TEntity, TEntityId, TResult, TQuery, TQueryValidator>> Logger { get; }

        protected IExpressionBuilder ExpressionBuilder { get; }
        
        /// <summary>
        /// Process the command.
        /// The command is validates prior to being processed
        /// </summary>
        /// <param name="query">command to run</param>
        /// <returns>The result of the command execution</returns>
        /// <exception cref="QueryNotValidException{TQueryId}">if  <paramref name="query"/> validation fails</exception>
        /// <exception cref="ArgumentNullException">if <paramref name="query"/> is <c>null</c></exception>
        public override async Task<TResult> HandleAsync(TQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            Logger.LogInformation($"Start executing query : {query.Id}");
            Logger.LogTrace("Validating query");
            IEnumerable<Task<ErrorInfo>> errorsTasks = Validator.Validate(query);
            IEnumerable<ErrorInfo> errors = await Task.WhenAll(errorsTasks).ConfigureAwait(false);
            if (errors.Any(item => item.Severity == Error))
            {
                Logger.LogTrace("validation failed", errors);
#if DEBUG || TRACE
                foreach (var error in errors)
                {
                    Logger.LogDebug($"{error.Key} - {error.Severity} : {error.Description}");
                }
#endif
                throw new QueryNotValidException<TQueryId>(query.Id, errors);

            }
            Logger.LogTrace("Query validation succeeded");

            using (var uow = UowFactory.New())
            {
                TEntityId data = query.Data;

                Expression<Func<TEntity, TResult>> selector = ExpressionBuilder.CreateMapExpression<TEntity, TResult>();
                TResult output = await uow.Repository<TEntity>().SingleOrDefaultAsync(selector, x => Equals(x.Id, data));
                
                Logger.LogInformation($"Query {query.Id} processed successfully");

                return output;
            }
        }
    }
}
