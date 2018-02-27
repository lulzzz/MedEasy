﻿using FluentAssertions;
using MedEasy.DAL.Repositories;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MedEasy.DAL.Tests
{
    public class PageTests
    {
        private ITestOutputHelper _outputTestHelper;

        public PageTests(ITestOutputHelper outputHelper)
        {
            _outputTestHelper = outputHelper;
        }


        [Fact]
        public void CtorWithNullEntriesShouldThrowArgumentNullException()
        {
            //Act
#pragma warning disable IDE0039 // Utiliser une fonction locale
            Action action = () => new Page<object>(null, 0, 0);
#pragma warning restore IDE0039 // Utiliser une fonction locale

            //Assert
            ArgumentNullException exception = action.Should().Throw<ArgumentNullException>().Which;
            exception.ParamName.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Default()
        {
            //Act
            Page<object> pagedResult = Page<object>.Default;

            //Assert
            pagedResult.Should().NotBeNull();
            pagedResult.Size.Should().Be(0);
            pagedResult.Count.Should().Be(0);
            pagedResult.Total.Should().Be(0);
            pagedResult.Entries.Should().BeEmpty();

        }


        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void CtorWithNegativePageSizeShouldThrowArgumentOutOfRangeException(int pageSize)
        {
            _outputTestHelper.WriteLine($"Page size : {pageSize}");

#pragma warning disable IDE0039 // Utiliser une fonction locale
            Action action = () => new Page<object>(Enumerable.Empty<object>(), 0, pageSize);
#pragma warning restore IDE0039 // Utiliser une fonction locale

            action.Should().Throw<ArgumentOutOfRangeException>().Which
                .ParamName.Should()
                    .BeEquivalentTo($"{nameof(Page<object>.Size)}");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void CtorWithNegativeTotalShouldThrowArgumentOutOfRangeException(int total)
        {
            _outputTestHelper.WriteLine($"{nameof(Page<object>.Total)} : {total}");

            //Act
#pragma warning disable IDE0039 // Utiliser une fonction locale
            Action action = () => new Page<object>(Enumerable.Empty<object>(), total, 1);
#pragma warning restore IDE0039 // Utiliser une fonction locale

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>().Which
                .ParamName.Should()
                    .BeEquivalentTo($"{nameof(Page<object>.Total)}");
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(1, 30, 1)]
        [InlineData(10, 5, 2)]
        [InlineData(12, 5, 3)]
        [InlineData(400, 30, 14)]
        public void CheckPageCount(int total, int pageSize, int expectedPageCount)
        {
            //Act
            Page<object> page = new Page<object>(Enumerable.Empty<object>(), total, pageSize);

            //Assert
            page.Count.Should().Be(expectedPageCount);
        }
    }
}
