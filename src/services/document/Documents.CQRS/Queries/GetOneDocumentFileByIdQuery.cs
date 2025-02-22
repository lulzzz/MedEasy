﻿namespace Documents.CQRS.Queries
{
    using Documents.DTO.v1;
    using Documents.Ids;

    using MedEasy.CQRS.Core.Queries;

    using System;
    using System.Collections.Generic;

    public class GetOneDocumentFileInfoByIdQuery : QueryBase<Guid, DocumentId, IAsyncEnumerable<DocumentPartInfo>>
    {
        public GetOneDocumentFileInfoByIdQuery(DocumentId data) : base(Guid.NewGuid(), data)
        {
        }
    }
}
