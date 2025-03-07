﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.Arrow;
using Microsoft.ML;
using Microsoft.ML.Data;
using Xunit;

namespace Microsoft.Data.Analysis.Tests
{
    public partial class DataFrameTests
    {
        public static DataFrame MakeDataFrameWithTwoColumns(int length, bool withNulls = true)
        {
            DataFrameColumn dataFrameColumn1 = new Int32DataFrameColumn("Int1", Enumerable.Range(0, length).Select(x => x));
            DataFrameColumn dataFrameColumn2 = new Int32DataFrameColumn("Int2", Enumerable.Range(10, length).Select(x => x));
            if (withNulls)
            {
                dataFrameColumn1[length / 2] = null;
                dataFrameColumn2[length / 2] = null;
            }
            DataFrame dataFrame = new DataFrame();
            dataFrame.Columns.Insert(0, dataFrameColumn1);
            dataFrame.Columns.Insert(1, dataFrameColumn2);
            return dataFrame;
        }

        public static ArrowStringDataFrameColumn CreateArrowStringColumn(int length, bool withNulls = true)
        {
            byte[] dataMemory = new byte[length * 3];
            byte[] nullMemory = new byte[BitUtility.ByteCount(length)];
            byte[] offsetMemory = new byte[(length + 1) * 4];

            // Initialize offset with 0 as the first value
            offsetMemory[0] = 0;
            offsetMemory[1] = 0;
            offsetMemory[2] = 0;
            offsetMemory[3] = 0;

            // Append "foo" length times, with a possible `null` in the middle
            int validStringsIndex = 0;
            for (int i = 0; i < length; i++)
            {
                if (withNulls && i == length / 2)
                {
                    BitUtility.SetBit(nullMemory, i, false);
                }
                else
                {
                    int dataMemoryIndex = validStringsIndex * 3;
                    dataMemory[dataMemoryIndex++] = 102;
                    dataMemory[dataMemoryIndex++] = 111;
                    dataMemory[dataMemoryIndex++] = 111;
                    BitUtility.SetBit(nullMemory, i, true);

                    validStringsIndex++;
                }

                // write the current length to (index + 1)
                int offsetIndex = (i + 1) * 4;
                int offsetValue = 3 * validStringsIndex;
                byte[] offsetValueBytes = BitConverter.GetBytes(offsetValue);
                offsetMemory[offsetIndex++] = offsetValueBytes[0];
                offsetMemory[offsetIndex++] = offsetValueBytes[1];
                offsetMemory[offsetIndex++] = offsetValueBytes[2];
                offsetMemory[offsetIndex++] = offsetValueBytes[3];
            }

            int nullCount = withNulls ? 1 : 0;
            return new ArrowStringDataFrameColumn("ArrowString", dataMemory, offsetMemory, nullMemory, length, nullCount);
        }

        public static VBufferDataFrameColumn<int> CreateVBufferDataFrame(int length)
        {
            var buffers = Enumerable.Repeat(new VBuffer<int>(5, new[] { 0, 1, 2, 3, 4 }), length).ToArray();
            return new VBufferDataFrameColumn<int>("VBuffer", buffers);
        }

        public static DataFrame MakeDataFrameWithAllColumnTypes(int length, bool withNulls = true)
        {
            DataFrame df = MakeDataFrameWithAllMutableAndArrowColumnTypes(length, withNulls);

            var vBufferColumn = CreateVBufferDataFrame(length);
            df.Columns.Insert(df.Columns.Count, vBufferColumn);

            return df;
        }

        public static DataFrame MakeDataFrameWithAllMutableAndArrowColumnTypes(int length, bool withNulls = true)
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(length, withNulls);
            DataFrameColumn arrowStringColumn = CreateArrowStringColumn(length, withNulls);
            df.Columns.Insert(df.Columns.Count, arrowStringColumn);

            return df;
        }

        public static DataFrame MakeDataFrameWithAllMutableColumnTypes(int length, bool withNulls = true)
        {
            DataFrame df = MakeDataFrameWithNumericStringAndDateTimeColumns(length, withNulls);
            DataFrameColumn boolColumn = new BooleanDataFrameColumn("Bool", Enumerable.Range(0, length).Select(x => x % 2 == 0));
            df.Columns.Insert(df.Columns.Count, boolColumn);
            if (withNulls)
            {
                boolColumn[length / 2] = null;
            }
            return df;
        }

        public static DataFrame MakeDataFrameWithNumericAndBoolColumns(int length, bool withNulls = true)
        {
            DataFrame df = MakeDataFrameWithNumericColumns(length, withNulls);
            DataFrameColumn boolColumn = new BooleanDataFrameColumn("Bool", Enumerable.Range(0, length).Select(x => x % 2 == 0));
            df.Columns.Insert(df.Columns.Count, boolColumn);
            if (withNulls)
            {
                boolColumn[length / 2] = null;
            }
            return df;
        }

        public static DataFrame MakeDataFrameWithNumericAndStringColumns(int length, bool withNulls = true)
        {
            DataFrame df = MakeDataFrameWithNumericColumns(length, withNulls);
            DataFrameColumn stringColumn = new StringDataFrameColumn("String", Enumerable.Range(0, length).Select(x => x.ToString()));
            df.Columns.Insert(df.Columns.Count, stringColumn);
            if (withNulls)
            {
                stringColumn[length / 2] = null;
            }

            DataFrameColumn charColumn = new CharDataFrameColumn("Char", Enumerable.Range(0, length).Select(x => (char)(x + 65)));
            df.Columns.Insert(df.Columns.Count, charColumn);
            if (withNulls)
            {
                charColumn[length / 2] = null;
            }
            return df;
        }

        internal static DateTime SampleDateTime = new DateTime(2021, 06, 04);
        public static DataFrame MakeDataFrameWithNumericStringAndDateTimeColumns(int length, bool withNulls = true)
        {
            DataFrame df = MakeDataFrameWithNumericAndStringColumns(length, withNulls);

            DataFrameColumn dateTimeColumn = new DateTimeDataFrameColumn("DateTime", Enumerable.Range(0, length).Select(x => SampleDateTime.AddDays(x)));
            df.Columns.Insert(df.Columns.Count, dateTimeColumn);
            if (withNulls)
            {
                dateTimeColumn[length / 2] = null;
            }
            return df;
        }

        public static DataFrame MakeDataFrameWithNumericColumns(int length, bool withNulls = true, int startingFrom = 0)
        {
            IEnumerable<int> range = Enumerable.Range(startingFrom, length);

            var byteColumn = new ByteDataFrameColumn("Byte", range.Select(x => (byte)x));
            var decimalColumn = new DecimalDataFrameColumn("Decimal", range.Select(x => (decimal)x));
            var doubleColumn = new DoubleDataFrameColumn("Double", range.Select(x => (double)x));
            var floatColumn = new SingleDataFrameColumn("Float", range.Select(x => (float)x));
            var intColumn = new Int32DataFrameColumn("Int", range.Select(x => x));
            var longColumn = new Int64DataFrameColumn("Long", range.Select(x => (long)x));
            var sbyteColumn = new SByteDataFrameColumn("Sbyte", range.Select(x => (sbyte)x));
            var shortColumn = new Int16DataFrameColumn("Short", range.Select(x => (short)x));
            var uintColumn = new UInt32DataFrameColumn("Uint", range.Select(x => (uint)x));
            var ulongColumn = new UInt64DataFrameColumn("Ulong", range.Select(x => (ulong)x));
            var ushortColumn = new UInt16DataFrameColumn("Ushort", range.Select(x => (ushort)x));

            var columnsList = new List<DataFrameColumn>
            {
                byteColumn,
                decimalColumn,
                doubleColumn,
                floatColumn,
                intColumn,
                longColumn,
                sbyteColumn,
                shortColumn,
                uintColumn,
                ulongColumn,
                ushortColumn
            };

            var dataFrame = new DataFrame(columnsList);

            if (withNulls)
            {
                for (var i = 0; i < dataFrame.Columns.Count; i++)
                {
                    dataFrame.Columns[i][length / 2] = null;
                }
            }

            return dataFrame;
        }

        public static DataFrame MakeDataFrame<T1, T2>(int length, bool withNulls = true)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            DataFrameColumn baseColumn1 = DataFrameColumn.Create("Column1", Enumerable.Range(0, length).Select(x => (T1)Convert.ChangeType(x % 2 == 0 ? 0 : 1, typeof(T1))));
            DataFrameColumn baseColumn2 = DataFrameColumn.Create("Column2", Enumerable.Range(0, length).Select(x => (T2)Convert.ChangeType(x % 2 == 0 ? 0 : 1, typeof(T2))));
            DataFrame dataFrame = new DataFrame(new List<DataFrameColumn> { baseColumn1, baseColumn2 });

            if (withNulls)
            {
                for (int i = 0; i < dataFrame.Columns.Count; i++)
                {
                    dataFrame.Columns[i][length / 2] = null;
                }
            }

            return dataFrame;
        }

        public DataFrame SplitTrainTest(DataFrame input, float testRatio, out DataFrame Test)
        {
            IEnumerable<int> randomIndices = Enumerable.Range(0, (int)input.Rows.Count);
            IEnumerable<int> trainIndices = randomIndices.Take((int)(input.Rows.Count * testRatio));
            IEnumerable<int> testIndices = randomIndices.Skip((int)(input.Rows.Count * testRatio));
            Test = input[testIndices];
            return input[trainIndices];
        }

        [Fact]
        public void TestVBufferColumn()
        {
            var vBufferColumn = CreateVBufferDataFrame(10);

            Assert.Equal(10, vBufferColumn.Length);
            Assert.Equal(5, vBufferColumn[0].GetValues().Length);
            Assert.Equal(0, vBufferColumn[0].GetValues()[0]);
        }

        [Fact]
        public void TestIndexer()
        {
            DataFrame dataFrame = MakeDataFrameWithTwoColumns(length: 10);
            var foo = dataFrame[0, 0];
            Assert.Equal(0, dataFrame[0, 0]);
            Assert.Equal(11, dataFrame[1, 1]);
            Assert.Equal(2, dataFrame.Columns.Count);
            Assert.Equal("Int1", dataFrame.Columns[0].Name);

            var headList = dataFrame.Head(5);
            Assert.Equal(14, (int)headList.Rows[4][1]);

            var tailList = dataFrame.Tail(5);
            Assert.Equal(19, (int)tailList.Rows[4][1]);

            dataFrame[2, 1] = 1000;
            Assert.Equal(1000, dataFrame[2, 1]);

            var row = dataFrame.Rows[4];
            Assert.Equal(14, (int)row[1]);

            var column = dataFrame["Int2"] as Int32DataFrameColumn;
            Assert.Equal(1000, (int)column[2]);

            Assert.Throws<ArgumentException>(() => dataFrame["Int5"]);
            Assert.Throws<ArgumentException>(() => dataFrame[(string)null]);
        }

        [Fact]
        public void ColumnAndTableCreationTest()
        {
            const int rowCount = 10;
            DataFrameColumn intColumn = new Int32DataFrameColumn("IntColumn", Enumerable.Range(0, rowCount).Select(x => x));
            DataFrameColumn floatColumn = new SingleDataFrameColumn("FloatColumn", Enumerable.Range(0, rowCount).Select(x => (float)x));
            DataFrame dataFrame = new DataFrame();
            dataFrame.Columns.Insert(0, intColumn);
            dataFrame.Columns.Insert(1, floatColumn);
            Assert.Equal(rowCount, dataFrame.Rows.Count);
            Assert.Equal(2, dataFrame.Columns.Count);
            Assert.Equal(2, dataFrame.Columns.LongCount());
            Assert.Equal(rowCount, dataFrame.Columns[0].Length);
            Assert.Equal("IntColumn", dataFrame.Columns[0].Name);
            Assert.Equal(rowCount, dataFrame.Columns[1].Length);
            Assert.Equal("FloatColumn", dataFrame.Columns[1].Name);

            //add column with bigger length than other columns in the dataframe
            DataFrameColumn bigColumn = new SingleDataFrameColumn("BigColumn", Enumerable.Range(0, rowCount + 1).Select(x => (float)x));
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.Insert(2, bigColumn));
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.Add(bigColumn));

            //add column smaller than other columns in the dataframe
            DataFrameColumn smallColumn = new SingleDataFrameColumn("SmallColumn", Enumerable.Range(0, rowCount - 1).Select(x => (float)x));
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.Insert(2, smallColumn));
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.Add(smallColumn));

            //add column with duplicate name
            DataFrameColumn repeatedName = new SingleDataFrameColumn("FloatColumn", Enumerable.Range(0, rowCount).Select(x => (float)x));
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.Insert(2, repeatedName));

            //Insert column at index out of range
            DataFrameColumn extraColumn = new SingleDataFrameColumn("OtherFloatColumn", Enumerable.Range(0, rowCount).Select(x => (float)x));
            var columnCount = dataFrame.Columns.Count;
            Assert.Throws<ArgumentOutOfRangeException>(() => dataFrame.Columns.Insert(columnCount + 1, repeatedName));

            Assert.Equal(2, dataFrame.Columns.Count);
            DataFrameColumn intColumnCopy = new Int32DataFrameColumn("IntColumn", Enumerable.Range(0, rowCount).Select(x => x));
            Assert.Throws<ArgumentException>(() => dataFrame.Columns[1] = intColumnCopy);

            DataFrameColumn differentIntColumn = new Int32DataFrameColumn("IntColumn1", Enumerable.Range(0, rowCount).Select(x => x));
            dataFrame.Columns[1] = differentIntColumn;
            Assert.True(object.ReferenceEquals(differentIntColumn, dataFrame.Columns[1]));

            dataFrame.Columns.RemoveAt(1);
            Assert.Single(dataFrame.Columns);
            Assert.True(ReferenceEquals(intColumn, dataFrame.Columns[0]));

            // Test the params constructor
            DataFrame dataFrame1 = new DataFrame(intColumn, floatColumn);
            Assert.Equal(2, dataFrame1.Columns.Count);
            Assert.Equal(intColumn, dataFrame1.Columns[0]);
            Assert.Equal(floatColumn, dataFrame1.Columns[1]);
        }

        [Fact]
        public void InsertAndRemoveColumnToTheEndOfNotEmptyDataFrameTests()
        {
            DataFrame dataFrame = MakeDataFrameWithAllMutableColumnTypes(10);
            DataFrameColumn intColumn = new Int32DataFrameColumn("NewIntColumn", Enumerable.Range(0, 10).Select(x => x));

            int columnCount = dataFrame.Columns.Count;
            DataFrameColumn originalLastColumn = dataFrame.Columns[columnCount - 1];

            //Insert new column at the end
            dataFrame.Columns.Insert(columnCount, intColumn);
            Assert.Equal(columnCount + 1, dataFrame.Columns.Count);

            //Remove first
            dataFrame.Columns.RemoveAt(0);
            Assert.Equal(columnCount, dataFrame.Columns.Count);

            //Check that int column was inserted
            DataFrameColumn intColumn_1 = dataFrame.Columns["NewIntColumn"];
            Assert.True(ReferenceEquals(intColumn, intColumn_1));

            //Check that last column of the original dataframe was not removed
            DataFrameColumn lastColumn_1 = dataFrame.Columns[originalLastColumn.Name];
            Assert.True(ReferenceEquals(originalLastColumn, lastColumn_1));

            //Check that new column is the last one
            int newIndex = dataFrame.Columns.IndexOf("NewIntColumn");
            Assert.Equal(columnCount - 1, newIndex);

            //Check that original last column now has correct index
            int newIndexForOriginalLastColumn = dataFrame.Columns.IndexOf(originalLastColumn.Name);
            Assert.Equal(columnCount - 2, newIndexForOriginalLastColumn);
        }

        [Fact]
        public void AddAndRemoveColumnToTheEmptyDataFrameTests()
        {
            DataFrame dataFrame = new DataFrame();
            DataFrameColumn intColumn = new Int32DataFrameColumn("NewIntColumn", Enumerable.Range(0, 10).Select(x => x));

            dataFrame.Columns.Add(intColumn);
            Assert.Single(dataFrame.Columns);
            Assert.Equal(10, dataFrame.Rows.Count);

            dataFrame.Columns.Remove(intColumn);
            Assert.Empty(dataFrame.Columns);
            Assert.Equal(0, dataFrame.Rows.Count);
        }

        [Fact]
        public void ClearColumnsTests()
        {
            //Arrange
            DataFrame dataFrame = MakeDataFrameWithAllMutableColumnTypes(10);

            //Act
            dataFrame.Columns.Clear();

            //Assert
            Assert.Empty(dataFrame.Columns);

            Assert.Equal(0, dataFrame.Rows.Count);
            Assert.Equal(0, dataFrame.Columns.LongCount());
        }

        [Fact]
        public void TestBinaryOperations()
        {
            DataFrame df = MakeDataFrameWithTwoColumns(12);
            IReadOnlyList<int> listOfInts = new List<int>() { 5, 5 };

            // The following binary ops return a copy
            var ret = df.Add(5);
            Assert.Equal(0, df[0, 0]);
            Assert.Equal(5, ret[0, 0]);
            ret = df.Add(listOfInts);
            Assert.Equal(0, df[0, 0]);
            Assert.Equal(5, ret[0, 0]);
            ret = df.Subtract(5);
            Assert.Equal(0, df[0, 0]);
            Assert.Equal(-5, ret[0, 0]);
            ret = df.Subtract(listOfInts);
            Assert.Equal(0, df[0, 0]);
            Assert.Equal(-5, ret[0, 0]);
            ret = df.Multiply(5);
            Assert.Equal(1, df[1, 0]);
            Assert.Equal(5, ret[1, 0]);
            ret = df.Multiply(listOfInts);
            Assert.Equal(1, df[1, 0]);
            Assert.Equal(5, ret[1, 0]);
            ret = df.Divide(5);
            Assert.Equal(5, df[5, 0]);
            Assert.Equal(1, ret[5, 0]);
            ret = df.Divide(listOfInts);
            Assert.Equal(5, df[5, 0]);
            Assert.Equal(1, ret[5, 0]);
            ret = df.Modulo(5);
            Assert.Equal(5, df[5, 0]);
            Assert.Equal(0, ret[5, 0]);
            ret = df.Modulo(listOfInts);
            Assert.Equal(5, df[5, 0]);
            Assert.Equal(0, ret[5, 0]);

            Assert.Equal(true, df.ElementwiseGreaterThanOrEqual(5)[7, 0]);
            Assert.Equal(true, df.ElementwiseGreaterThanOrEqual(listOfInts)[7, 0]);
            Assert.Equal(true, df.ElementwiseLessThanOrEqual(5)[4, 0]);
            Assert.Equal(true, df.ElementwiseLessThanOrEqual(listOfInts)[4, 0]);
            Assert.Equal(false, df.ElementwiseGreaterThan(5)[5, 0]);
            Assert.Equal(false, df.ElementwiseGreaterThan(listOfInts)[5, 0]);
            Assert.Equal(false, df.ElementwiseLessThan(5)[5, 0]);
            Assert.Equal(false, df.ElementwiseLessThan(listOfInts)[5, 0]);
            // The following binary ops are in place
            Assert.Equal(5, df.Add(5, inPlace: true)[0, 0]);
            Assert.Equal(10, df.Add(listOfInts, inPlace: true)[0, 0]);
            Assert.Equal(5, df.Subtract(5, inPlace: true)[0, 0]);
            Assert.Equal(0, df.Subtract(listOfInts, inPlace: true)[0, 0]);
            Assert.Equal(5, df.Multiply(5, inPlace: true)[1, 0]);
            Assert.Equal(25, df.Multiply(listOfInts, inPlace: true)[1, 0]);
            Assert.Equal(5, df.Divide(5, inPlace: true)[1, 0]);
            Assert.Equal(1, df.Divide(listOfInts, inPlace: true)[1, 0]);
            Assert.Equal(1, df.Modulo(5, inPlace: true)[1, 0]);
            Assert.Equal(1, df.Modulo(listOfInts, inPlace: true)[1, 0]);
            Assert.Equal(2, df.LeftShift(1)[1, 0]);
            Assert.Equal(1, df.RightShift(1)[2, 0]);
        }

        [Fact]
        public void TestBinaryOperationsWithColumns()
        {
            int length = 10;
            var df1 = MakeDataFrameWithNumericColumns(length);
            var df2 = MakeDataFrameWithNumericColumns(length);

            DataFrameColumn newColumn;
            DataFrameColumn verify;
            for (int i = 0; i < df1.Columns.Count; i++)
            {
                newColumn = df1.Columns[df1.Columns[i].Name] + df2.Columns[df2.Columns[i].Name];
                verify = newColumn.ElementwiseEquals(df1.Columns[i] * 2);
                Assert.Equal(true, verify[0]);

                newColumn = df1.Columns[df1.Columns[i].Name] - df2.Columns[df2.Columns[i].Name];
                verify = newColumn.ElementwiseEquals(0);
                Assert.Equal(true, verify[0]);

                newColumn = df1.Columns[df1.Columns[i].Name] * df2.Columns[df2.Columns[i].Name];
                verify = newColumn.ElementwiseEquals(df1.Columns[i] * df1.Columns[i]);
                Assert.Equal(true, verify[0]);

                var df1Column = df1.Columns[i] + 1;
                var df2Column = df2.Columns[i] + 1;
                newColumn = df1Column / df2Column;
                verify = newColumn.ElementwiseEquals(1);
                Assert.Equal(true, verify[0]);

                newColumn = df1Column % df2Column;
                verify = newColumn.ElementwiseEquals(0);
                Assert.Equal(true, verify[0]);

                verify = df1.Columns[df1.Columns[i].Name].ElementwiseEquals(df2.Columns[df2.Columns[i].Name]);
                Assert.True(verify.All());

                verify = df1.Columns[df1.Columns[i].Name].ElementwiseNotEquals(df2.Columns[df2.Columns[i].Name]);
                Assert.False(verify.Any());

                verify = df1.Columns[df1.Columns[i].Name].ElementwiseGreaterThanOrEqual(df2.Columns[df2.Columns[i].Name]);
                Assert.True(verify.All());

                verify = df1.Columns[df1.Columns[i].Name].ElementwiseLessThanOrEqual(df2.Columns[df2.Columns[i].Name]);
                Assert.True(verify.All());

                verify = df1.Columns[df1.Columns[i].Name].ElementwiseGreaterThan(df2.Columns[df2.Columns[i].Name]);
                Assert.False(verify.Any());

                verify = df1.Columns[df1.Columns[i].Name].ElementwiseLessThan(df2.Columns[df2.Columns[i].Name]);
                Assert.False(verify.Any());
            }
        }

        [Fact]
        public void TestBinaryOperationsWithConversions()
        {
            DataFrame df = DataFrameTests.MakeDataFrameWithTwoColumns(10);

            // Add a double to an int column
            DataFrame dfd = df.Add(5.0f);
            var dtype = dfd.Columns[0].DataType;
            Assert.True(dtype == typeof(double));

            // Add a decimal to an int column
            DataFrame dfm = df.Add(5.0m);
            dtype = dfm.Columns[0].DataType;
            Assert.True(dtype == typeof(decimal));

            // int + bool should throw
            Assert.Throws<NotSupportedException>(() => df.Add(true));

            var dataFrameColumn1 = new DoubleDataFrameColumn("Double1", Enumerable.Range(0, 10).Select(x => (double)x));
            df.Columns[0] = dataFrameColumn1;
            // Double + comparison ops should throw
            Assert.Throws<NotSupportedException>(() => df.And(true));
        }

        [Fact]
        public void TestBinaryOperationsOnBoolColumn()
        {
            var df = new DataFrame();
            var dataFrameColumn1 = new BooleanDataFrameColumn("Bool1", Enumerable.Range(0, 10).Select(x => true));
            var dataFrameColumn2 = new BooleanDataFrameColumn("Bool2", Enumerable.Range(0, 10).Select(x => true));
            df.Columns.Insert(0, dataFrameColumn1);
            df.Columns.Insert(1, dataFrameColumn2);

            // bool + int should throw
            Assert.Throws<NotSupportedException>(() => df.Add(5));
            // Left shift should throw
            Assert.Throws<NotSupportedException>(() => df.LeftShift(5));

            IReadOnlyList<bool> listOfBools = new List<bool>() { true, false };
            // boolean and And should work
            var newdf = df.And(true);
            Assert.Equal(true, newdf[4, 0]);
            var newdf1 = df.And(listOfBools);
            Assert.Equal(false, newdf1[4, 1]);

            newdf = df.Or(true);
            Assert.Equal(true, newdf[4, 0]);
            newdf1 = df.Or(listOfBools);
            Assert.Equal(true, newdf1[4, 1]);

            newdf = df.Xor(true);
            Assert.Equal(false, newdf[4, 0]);
            newdf1 = df.Xor(listOfBools);
            Assert.Equal(true, newdf1[4, 1]);
        }

        [Fact]
        public void TestBinaryOperationsOnDateTimeColumn()
        {
            var df = new DataFrame();
            var dataFrameColumn1 = new DateTimeDataFrameColumn("DateTime1", Enumerable.Range(0, 5).Select(x => SampleDateTime.AddDays(x)));
            // Make the second data frame column have one value that is different
            var dataFrameColumn2 = new DateTimeDataFrameColumn("DateTime2", Enumerable.Range(0, 4).Select(x => SampleDateTime.AddDays(x)));
            dataFrameColumn2.Append(SampleDateTime.AddDays(6));
            df.Columns.Insert(0, dataFrameColumn1);
            df.Columns.Insert(1, dataFrameColumn2);

            // DateTime + int should throw
            Assert.Throws<NotSupportedException>(() => df.Add(5));
            // Left shift should throw
            Assert.Throws<NotSupportedException>(() => df.LeftShift(5));
            // Right shift should throw
            Assert.Throws<NotSupportedException>(() => df.RightShift(5));

            // And should throw
            Assert.Throws<NotSupportedException>(() => df.And(true));
            // Or should throw
            Assert.Throws<NotSupportedException>(() => df.Or(true));
            // Xor should throw
            Assert.Throws<NotSupportedException>(() => df.Xor(true));

            var equalsResult = dataFrameColumn1.ElementwiseEquals(dataFrameColumn2);
            Assert.True(equalsResult[0]);
            Assert.False(equalsResult[4]);

            var notEqualsResult = dataFrameColumn1.ElementwiseNotEquals(dataFrameColumn2);
            Assert.False(notEqualsResult[0]);
            Assert.True(notEqualsResult[4]);
        }

        [Fact]
        public void TestBinaryOperationsOnArrowStringColumn()
        {
            var df = new DataFrame();
            var strArrayBuilder = new StringArray.Builder();
            for (int i = 0; i < 10; i++)
            {
                strArrayBuilder.Append(i.ToString());
            }
            StringArray strArray = strArrayBuilder.Build();

            ArrowStringDataFrameColumn stringColumn = new ArrowStringDataFrameColumn("String", strArray.ValueBuffer.Memory, strArray.ValueOffsetsBuffer.Memory, strArray.NullBitmapBuffer.Memory, strArray.Length, strArray.NullCount);
            df.Columns.Insert(0, stringColumn);

            DataFrameColumn newCol = stringColumn.ElementwiseEquals(4);
            Assert.Equal(true, newCol[4]);
            Assert.Equal(false, newCol[0]);
            Assert.Equal(false, newCol[5]);

            newCol = stringColumn.ElementwiseEquals("4");
            Assert.Equal(true, newCol[4]);
            Assert.Equal(false, newCol[0]);

            newCol = stringColumn.ElementwiseEquals("foo");
            Assert.False(newCol.All());
            newCol = stringColumn.ElementwiseEquals(null);
            Assert.False(newCol.All());

            ArrowStringDataFrameColumn stringColumnCopy = new ArrowStringDataFrameColumn("String", strArray.ValueBuffer.Memory, strArray.ValueOffsetsBuffer.Memory, strArray.NullBitmapBuffer.Memory, strArray.Length, strArray.NullCount);
            newCol = stringColumn.ElementwiseEquals(stringColumnCopy);
            Assert.True(newCol.All());

            DataFrameColumn stringColumnCopyAsBaseColumn = stringColumnCopy;
            newCol = stringColumn.ElementwiseEquals(stringColumnCopyAsBaseColumn);
            Assert.True(newCol.All());

            newCol = stringColumn.ElementwiseNotEquals(5);
            Assert.Equal(true, newCol[0]);
            Assert.Equal(false, newCol[5]);

            newCol = stringColumn.ElementwiseNotEquals("5");
            Assert.Equal(true, newCol[0]);
            Assert.Equal(false, newCol[5]);

            newCol = stringColumn.ElementwiseNotEquals("foo");
            Assert.True(newCol.All());
            newCol = stringColumn.ElementwiseNotEquals(null);
            Assert.True(newCol.All());

            newCol = stringColumn.ElementwiseNotEquals(stringColumnCopy);
            Assert.False(newCol.All());

            newCol = stringColumn.ElementwiseNotEquals(stringColumnCopyAsBaseColumn);
            Assert.False(newCol.All());
        }

        [Fact]
        public void TestBinaryOperationsOnStringColumn()
        {
            var df = new DataFrame();
            DataFrameColumn stringColumn = new StringDataFrameColumn("String", Enumerable.Range(0, 10).Select(x => x.ToString()));
            df.Columns.Insert(0, stringColumn);

            DataFrameColumn newCol = stringColumn.ElementwiseEquals(5);
            Assert.Equal(true, newCol[5]);
            Assert.Equal(false, newCol[0]);

            newCol = (stringColumn as StringDataFrameColumn).ElementwiseEquals("5");
            Assert.Equal(true, newCol[5]);
            Assert.Equal(false, newCol[0]);

            DataFrameColumn stringColumnCopy = new StringDataFrameColumn("String", Enumerable.Range(0, 10).Select(x => x.ToString()));
            newCol = stringColumn.ElementwiseEquals(stringColumnCopy);
            Assert.Equal(true, newCol[5]);
            Assert.Equal(true, newCol[0]);

            StringDataFrameColumn typedStringColumn = stringColumn as StringDataFrameColumn;
            StringDataFrameColumn typedStringColumnCopy = stringColumnCopy as StringDataFrameColumn;
            newCol = typedStringColumn.ElementwiseEquals(typedStringColumnCopy);
            Assert.True(newCol.All());

            newCol = stringColumn.ElementwiseNotEquals(5);
            Assert.Equal(false, newCol[5]);
            Assert.Equal(true, newCol[0]);

            newCol = typedStringColumn.ElementwiseNotEquals("5");
            Assert.Equal(false, newCol[5]);
            Assert.Equal(true, newCol[0]);

            newCol = stringColumn.ElementwiseNotEquals(stringColumnCopy);
            Assert.Equal(false, newCol[5]);
            Assert.Equal(false, newCol[0]);

            newCol = typedStringColumn.ElementwiseNotEquals(typedStringColumnCopy);
            Assert.False(newCol.All());

            newCol = typedStringColumn.Add("suffix");
            for (int i = 0; i < newCol.Length; i++)
            {
                Assert.Equal(newCol[i], typedStringColumn[i] + "suffix");
            }
            DataFrameColumn addString = typedStringColumn + "suffix";
            for (int i = 0; i < addString.Length; i++)
            {
                Assert.Equal(addString[i], typedStringColumn[i] + "suffix");
            }
            Assert.True(newCol.ElementwiseEquals(addString).All());
            addString = "prefix" + typedStringColumn;
            for (int i = 0; i < addString.Length; i++)
            {
                Assert.Equal(addString[i], "prefix" + typedStringColumn[i]);
            }
        }

        [Fact]
        public void TestBinaryOperatorsWithConversions()
        {
            var df = MakeDataFrameWithNumericColumns(10);

            DataFrame tempDf = df + 1;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] + (double)1);
            tempDf = df + 1.1;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] + 1.1);
            tempDf = df + 1.1m;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] + 1.1m);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = df - 1.1;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] - 1.1);
            tempDf = df - 1.1m;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] - 1.1m);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = df * 1.1;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] * 1.1);
            tempDf = df * 1.1m;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] * 1.1m);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = df / 1.1;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] / 1.1);
            tempDf = df / 1.1m;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] / 1.1m);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = df % 1.1;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] % 1.1);
            tempDf = df % 1.1m;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] % 1.1m);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = 1 + df;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] + (double)1);
            tempDf = 1.1 + df;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] + 1.1);
            tempDf = 1.1m + df;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] + 1.1m);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = 1.1 - df;
            Assert.Equal(tempDf[0, 0], 1.1 - (byte)df[0, 0]);
            tempDf = 1.1m - df;
            Assert.Equal(tempDf[0, 0], 1.1m - (byte)df[0, 0]);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = 1.1 * df;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] * 1.1);
            tempDf = 1.1m * df;
            Assert.Equal(tempDf[0, 0], (byte)df[0, 0] * 1.1m);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            // To prevent a divide by zero
            var plusOne = df + 1;
            tempDf = 1.1 / plusOne;
            Assert.Equal(tempDf[0, 0], 1.1 / (double)plusOne[0, 0]);
            var plusDecimal = df + 1.1m;
            tempDf = 1.1m / plusDecimal;
            Assert.Equal(tempDf[0, 0], (1.1m) / (decimal)plusDecimal[0, 0]);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            tempDf = 1.1 % plusOne;
            Assert.Equal(tempDf[0, 0], 1.1 % (double)plusOne[0, 0]);
            tempDf = 1.1m % plusDecimal;
            Assert.Equal(tempDf[0, 0], 1.1m % (decimal)plusDecimal[0, 0]);
            Assert.True(typeof(decimal) == tempDf.Columns["Int"].DataType);

            Assert.Equal((byte)0, df[0, 0]);
        }

        [Fact]
        public void TestBinaryOperationsOnColumns()
        {
            Int32DataFrameColumn column = new Int32DataFrameColumn("Int", Enumerable.Range(0, 10));
            Assert.ThrowsAny<ArgumentException>(() => column.Add(5.5, inPlace: true));
            Assert.ThrowsAny<ArgumentException>(() => column.ReverseAdd(5.5, inPlace: true));
            string str = "A String";
            Assert.ThrowsAny<ArgumentException>(() => column.Add(str, inPlace: true));
            Assert.ThrowsAny<ArgumentException>(() => column.ReverseAdd(str, inPlace: true));
        }

        [Fact]
        public void TestColumnReverseOrderState()
        {
            var column = new Int32DataFrameColumn("Int", Enumerable.Range(0, 10));
            var newColumn = 1 - column;
            var checkOrderColumn = 1 - newColumn;
            Assert.True(checkOrderColumn.ElementwiseEquals(column).All());
        }

        [Fact]
        public void TestProjectionAndAppend()
        {
            DataFrame df = MakeDataFrameWithTwoColumns(10);

            df["Int3"] = df.Columns["Int1"] * 2 + df.Columns["Int2"];
            Assert.Equal(16, df.Columns["Int3"][2]);
        }

        [Fact]
        public void TestComputations()
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(10);
            df["Int"][0] = -10;
            Assert.Equal(-10, df.Columns["Int"][0]);

            DataFrameColumn absColumn = df.Columns["Int"].Abs();
            Assert.Equal(10, absColumn[0]);
            Assert.Equal(-10, df.Columns["Int"][0]);
            df.Columns["Int"].Abs(true);
            Assert.Equal(10, df.Columns["Int"][0]);

            Assert.Throws<NotSupportedException>(() => df.Columns["Byte"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Byte"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Char"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Char"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["DateTime"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["DateTime"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Decimal"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Decimal"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Double"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Double"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Float"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Float"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Int"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Int"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Long"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Long"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Sbyte"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Sbyte"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Short"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Short"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Uint"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Uint"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ulong"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ulong"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ushort"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ushort"].Any());

            bool any = df.Columns["Bool"].Any();
            bool all = df.Columns["Bool"].All();
            Assert.True(any);
            Assert.False(all);

            // Test the computation results
            df.Columns["Double"][0] = 100.0;
            DataFrameColumn doubleColumn = df.Columns["Double"].CumulativeMax();
            for (int i = 0; i < doubleColumn.Length; i++)
            {
                if (i == 5)
                    Assert.Null(doubleColumn[i]);
                else
                    Assert.Equal(100.0, (double)doubleColumn[i]);
            }
            Assert.Equal(1.0, df.Columns["Double"][1]);
            df.Columns["Double"].CumulativeMax(true);
            for (int i = 0; i < df.Columns["Double"].Length; i++)
            {
                if (i == 5)
                    Assert.Null(df.Columns["Double"][i]);
                else
                    Assert.Equal(100.0, (double)df.Columns["Double"][i]);
            }

            df.Columns["Float"][0] = -10.0f;
            DataFrameColumn floatColumn = df.Columns["Float"].CumulativeMin();
            for (int i = 0; i < floatColumn.Length; i++)
            {
                if (i == 5)
                    Assert.Null(floatColumn[i]);
                else
                    Assert.Equal(-10.0f, (float)floatColumn[i]);
            }
            Assert.Equal(9.0f, df.Columns["Float"][9]);
            df.Columns["Float"].CumulativeMin(true);
            for (int i = 0; i < df.Columns["Float"].Length; i++)
            {
                if (i == 5)
                    Assert.Null(df.Columns["Float"][i]);
                else
                    Assert.Equal(-10.0f, (float)df.Columns["Float"][i]);
            }

            DataFrameColumn uintColumn = df.Columns["Uint"].CumulativeProduct();
            Assert.Equal((uint)0, uintColumn[8]);
            Assert.Equal((uint)8, df.Columns["Uint"][8]);
            df.Columns["Uint"].CumulativeProduct(true);
            Assert.Equal((uint)0, df.Columns["Uint"][9]);

            DataFrameColumn ushortColumn = df.Columns["Ushort"].CumulativeSum();
            Assert.Equal((ushort)40, ushortColumn[9]);
            Assert.Equal((ushort)9, df.Columns["Ushort"][9]);
            df.Columns["Ushort"].CumulativeSum(true);
            Assert.Equal((ushort)40, df.Columns["Ushort"][9]);

            Assert.Equal(100.0, df.Columns["Double"].Max());
            Assert.Equal(-10.0f, df.Columns["Float"].Min());
            Assert.Equal((uint)0, df.Columns["Uint"].Product());
            Assert.Equal((ushort)130, df.Columns["Ushort"].Sum());

            df.Columns["Double"][0] = 100.1;
            Assert.Equal(100.1, df.Columns["Double"][0]);
            DataFrameColumn roundColumn = df.Columns["Double"].Round();
            Assert.Equal(100.0, roundColumn[0]);
            Assert.Equal(100.1, df.Columns["Double"][0]);
            df.Columns["Double"].Round(true);
            Assert.Equal(100.0, df.Columns["Double"][0]);

            // Test that none of the numeric column types throw
            for (int i = 0; i < df.Columns.Count; i++)
            {
                DataFrameColumn column = df.Columns[i];
                if (column.DataType == typeof(bool))
                {
                    Assert.Throws<NotSupportedException>(() => column.CumulativeMax());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeMin());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeProduct());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeSum());
                    Assert.Throws<NotSupportedException>(() => column.Max());
                    Assert.Throws<NotSupportedException>(() => column.Min());
                    Assert.Throws<NotSupportedException>(() => column.Product());
                    Assert.Throws<NotSupportedException>(() => column.Sum());
                    continue;
                }
                else if (column.DataType == typeof(string))
                {
                    Assert.Throws<NotImplementedException>(() => column.CumulativeMax());
                    Assert.Throws<NotImplementedException>(() => column.CumulativeMin());
                    Assert.Throws<NotImplementedException>(() => column.CumulativeProduct());
                    Assert.Throws<NotImplementedException>(() => column.CumulativeSum());
                    Assert.Throws<NotImplementedException>(() => column.Max());
                    Assert.Throws<NotImplementedException>(() => column.Min());
                    Assert.Throws<NotImplementedException>(() => column.Product());
                    Assert.Throws<NotImplementedException>(() => column.Sum());
                    continue;
                }
                else if (column.DataType == typeof(DateTime))
                {
                    column.CumulativeMax();
                    column.CumulativeMin();
                    column.Max();
                    column.Min();

                    Assert.Throws<NotSupportedException>(() => column.CumulativeProduct());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeSum());
                    Assert.Throws<NotSupportedException>(() => column.Product());
                    Assert.Throws<NotSupportedException>(() => column.Sum());
                    continue;
                }

                column.CumulativeMax();
                column.CumulativeMin();
                column.CumulativeProduct();
                column.CumulativeSum();
                column.Max();
                column.Min();
                column.Product();
                column.Sum();
            }
        }

        [Fact]
        public void TestComputationsIncludingDateTime()
        {
            DataFrame df = MakeDataFrameWithNumericStringAndDateTimeColumns(10);
            df["Int"][0] = -10;
            Assert.Equal(-10, df.Columns["Int"][0]);

            DataFrameColumn absColumn = df.Columns["Int"].Abs();
            Assert.Equal(10, absColumn[0]);
            Assert.Equal(-10, df.Columns["Int"][0]);
            df.Columns["Int"].Abs(true);
            Assert.Equal(10, df.Columns["Int"][0]);

            Assert.Throws<NotSupportedException>(() => df.Columns["Byte"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Byte"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Char"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Char"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Decimal"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Decimal"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Double"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Double"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Float"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Float"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Int"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Int"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Long"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Long"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Sbyte"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Sbyte"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Short"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Short"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Uint"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Uint"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ulong"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ulong"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ushort"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["Ushort"].Any());
            Assert.Throws<NotSupportedException>(() => df.Columns["DateTime"].All());
            Assert.Throws<NotSupportedException>(() => df.Columns["DateTime"].Any());

            // Test the computation results
            var maxDate = SampleDateTime.AddDays(100);
            df.Columns["DateTime"][0] = maxDate;
            DataFrameColumn dateTimeColumn = df.Columns["DateTime"].CumulativeMax();
            for (int i = 0; i < dateTimeColumn.Length; i++)
            {
                if (i == 5)
                    Assert.Null(dateTimeColumn[i]);
                else
                    Assert.Equal(maxDate, (DateTime)dateTimeColumn[i]);
            }
            Assert.Equal(maxDate, dateTimeColumn.Max());

            df.Columns["Double"][0] = 100.0;
            DataFrameColumn doubleColumn = df.Columns["Double"].CumulativeMax();
            for (int i = 0; i < doubleColumn.Length; i++)
            {
                if (i == 5)
                    Assert.Null(doubleColumn[i]);
                else
                    Assert.Equal(100.0, (double)doubleColumn[i]);
            }
            Assert.Equal(1.0, df.Columns["Double"][1]);
            df.Columns["Double"].CumulativeMax(true);
            for (int i = 0; i < df.Columns["Double"].Length; i++)
            {
                if (i == 5)
                    Assert.Null(df.Columns["Double"][i]);
                else
                    Assert.Equal(100.0, (double)df.Columns["Double"][i]);
            }

            df.Columns["Float"][0] = -10.0f;
            DataFrameColumn floatColumn = df.Columns["Float"].CumulativeMin();
            for (int i = 0; i < floatColumn.Length; i++)
            {
                if (i == 5)
                    Assert.Null(floatColumn[i]);
                else
                    Assert.Equal(-10.0f, (float)floatColumn[i]);
            }
            Assert.Equal(9.0f, df.Columns["Float"][9]);
            df.Columns["Float"].CumulativeMin(true);
            for (int i = 0; i < df.Columns["Float"].Length; i++)
            {
                if (i == 5)
                    Assert.Null(df.Columns["Float"][i]);
                else
                    Assert.Equal(-10.0f, (float)df.Columns["Float"][i]);
            }

            DataFrameColumn uintColumn = df.Columns["Uint"].CumulativeProduct();
            Assert.Equal((uint)0, uintColumn[8]);
            Assert.Equal((uint)8, df.Columns["Uint"][8]);
            df.Columns["Uint"].CumulativeProduct(true);
            Assert.Equal((uint)0, df.Columns["Uint"][9]);

            DataFrameColumn ushortColumn = df.Columns["Ushort"].CumulativeSum();
            Assert.Equal((ushort)40, ushortColumn[9]);
            Assert.Equal((ushort)9, df.Columns["Ushort"][9]);
            df.Columns["Ushort"].CumulativeSum(true);
            Assert.Equal((ushort)40, df.Columns["Ushort"][9]);

            Assert.Equal(100.0, df.Columns["Double"].Max());
            Assert.Equal(-10.0f, df.Columns["Float"].Min());
            Assert.Equal((uint)0, df.Columns["Uint"].Product());
            Assert.Equal((ushort)130, df.Columns["Ushort"].Sum());

            df.Columns["Double"][0] = 100.1;
            Assert.Equal(100.1, df.Columns["Double"][0]);
            DataFrameColumn roundColumn = df.Columns["Double"].Round();
            Assert.Equal(100.0, roundColumn[0]);
            Assert.Equal(100.1, df.Columns["Double"][0]);
            df.Columns["Double"].Round(true);
            Assert.Equal(100.0, df.Columns["Double"][0]);

            // Test that none of the numeric column types throw
            for (int i = 0; i < df.Columns.Count; i++)
            {
                DataFrameColumn column = df.Columns[i];
                if (column.DataType == typeof(bool))
                {
                    Assert.Throws<NotSupportedException>(() => column.CumulativeMax());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeMin());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeProduct());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeSum());
                    Assert.Throws<NotSupportedException>(() => column.Max());
                    Assert.Throws<NotSupportedException>(() => column.Min());
                    Assert.Throws<NotSupportedException>(() => column.Product());
                    Assert.Throws<NotSupportedException>(() => column.Sum());
                    continue;
                }
                else if (column.DataType == typeof(string))
                {
                    Assert.Throws<NotImplementedException>(() => column.CumulativeMax());
                    Assert.Throws<NotImplementedException>(() => column.CumulativeMin());
                    Assert.Throws<NotImplementedException>(() => column.CumulativeProduct());
                    Assert.Throws<NotImplementedException>(() => column.CumulativeSum());
                    Assert.Throws<NotImplementedException>(() => column.Max());
                    Assert.Throws<NotImplementedException>(() => column.Min());
                    Assert.Throws<NotImplementedException>(() => column.Product());
                    Assert.Throws<NotImplementedException>(() => column.Sum());
                    continue;
                }
                else if (column.DataType == typeof(DateTime))
                {
                    Assert.Throws<NotSupportedException>(() => column.CumulativeProduct());
                    Assert.Throws<NotSupportedException>(() => column.CumulativeSum());
                    Assert.Throws<NotSupportedException>(() => column.Product());
                    Assert.Throws<NotSupportedException>(() => column.Sum());
                    continue;
                }
                column.CumulativeMax();
                column.CumulativeMin();
                column.CumulativeProduct();
                column.CumulativeSum();
                column.Max();
                column.Min();
                column.Product();
                column.Sum();
            }
        }

        [Theory]
        [InlineData(5, 10)]
        [InlineData(-15, 10)]
        [InlineData(-5, 10)]
        public void TestComputations_WithNegativeNumbers_MaxMin_Calculated(int startingFrom, int length)
        {
            // Arrange

            IEnumerable<int> range = Enumerable.Range(startingFrom, length);

            int max = range.Max();
            int min = range.Min();

            DataFrame df = MakeDataFrameWithNumericColumns(length, withNulls: false, startingFrom);

            var byteColumn = (PrimitiveDataFrameColumn<byte>)df.Columns["Byte"];
            var decimalColumn = (PrimitiveDataFrameColumn<decimal>)df.Columns["Decimal"];
            var doubleColumn = (PrimitiveDataFrameColumn<double>)df.Columns["Double"];
            var floatColumn = (PrimitiveDataFrameColumn<float>)df.Columns["Float"];
            var intColumn = (PrimitiveDataFrameColumn<int>)df.Columns["Int"];
            var longColumn = (PrimitiveDataFrameColumn<long>)df.Columns["Long"];
            var sbyteColumn = (PrimitiveDataFrameColumn<sbyte>)df.Columns["Sbyte"];
            var shortColumn = (PrimitiveDataFrameColumn<short>)df.Columns["Short"];
            var uintColumn = (PrimitiveDataFrameColumn<uint>)df.Columns["Uint"];
            var ulongColumn = (PrimitiveDataFrameColumn<ulong>)df.Columns["Ulong"];
            var ushortColumn = (PrimitiveDataFrameColumn<ushort>)df.Columns["Ushort"];

            // Act, Assert

            // We need to iterate over all range with conversion to byte due to negative numbers issue
            Assert.Equal((byte)byteColumn.Max(), range.Select(x => (byte)x).Max());

            Assert.Equal((decimal)decimalColumn.Max(), (decimal)max);
            Assert.Equal((double)doubleColumn.Max(), (double)max);
            Assert.Equal((float)floatColumn.Max(), (float)max);
            Assert.Equal((int)intColumn.Max(), (int)max);
            Assert.Equal((long)longColumn.Max(), (long)max);
            Assert.Equal((sbyte)sbyteColumn.Max(), (sbyte)max);
            Assert.Equal((short)shortColumn.Max(), (short)max);

            // We need to iterate over all range with conversion to uint due to negative numbers issue
            Assert.Equal((uint)uintColumn.Max(), range.Select(x => (uint)x).Max());

            // We need to iterate over all range with conversion to ulong due to negative numbers issue
            Assert.Equal((ulong)ulongColumn.Max(), range.Select(x => (ulong)x).Max());

            // We need to iterate over all range with conversion to ushort due to negative numbers issue
            Assert.Equal((ushort)ushortColumn.Max(), range.Select(x => (ushort)x).Max());

            // We need to iterate over all range with conversion to byte due to negative numbers issue
            Assert.Equal((byte)byteColumn.Min(), range.Select(x => (byte)x).Min());

            Assert.Equal((decimal)decimalColumn.Min(), (decimal)min);
            Assert.Equal((double)doubleColumn.Min(), (double)min);
            Assert.Equal((float)floatColumn.Min(), (float)min);
            Assert.Equal((int)intColumn.Min(), (int)min);
            Assert.Equal((long)longColumn.Min(), (long)min);
            Assert.Equal((sbyte)sbyteColumn.Min(), (sbyte)min);
            Assert.Equal((short)shortColumn.Min(), (short)min);

            // We need to iterate over all range with conversion to uint due to negative numbers issue
            Assert.Equal((uint)uintColumn.Min(), range.Select(x => (uint)x).Min());

            // We need to iterate over all range with conversion to ulong due to negative numbers issue
            Assert.Equal((ulong)ulongColumn.Min(), range.Select(x => (ulong)x).Min());

            // We need to iterate over all range with conversion to ushort due to negative numbers issue
            Assert.Equal((ushort)ushortColumn.Min(), range.Select(x => (ushort)x).Min());
        }

        [Fact]
        public void TestOrderBy()
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(20);
            df.Columns["Int"][0] = 100;
            df.Columns["Int"][19] = -1;
            df.Columns["Int"][5] = 2000;

            // Sort by "Int" in ascending order
            var sortedDf = df.OrderBy("Int");
            Assert.Null(sortedDf.Columns["Int"][19]);
            Assert.Equal(-1, sortedDf.Columns["Int"][0]);
            Assert.Equal(100, sortedDf.Columns["Int"][17]);
            Assert.Equal(2000, sortedDf.Columns["Int"][18]);

            // Sort by "Int" in descending order
            sortedDf = df.OrderByDescending("Int");
            Assert.Null(sortedDf.Columns["Int"][0]);
            Assert.Equal(-1, sortedDf.Columns["Int"][19]);
            Assert.Equal(100, sortedDf.Columns["Int"][2]);
            Assert.Equal(2000, sortedDf.Columns["Int"][1]);

            // Sort by "String" in ascending order
            sortedDf = df.OrderBy("String");
            Assert.Null(sortedDf.Columns["Int"][19]);
            Assert.Equal(1, sortedDf.Columns["Int"][1]);
            Assert.Equal(8, sortedDf.Columns["Int"][17]);
            Assert.Equal(9, sortedDf.Columns["Int"][18]);

            // Sort by "String" in descending order
            sortedDf = df.OrderByDescending("String");
            Assert.Null(sortedDf.Columns["Int"][0]);
            Assert.Equal(8, sortedDf.Columns["Int"][2]);
            Assert.Equal(9, sortedDf.Columns["Int"][1]);
        }

        [Fact]
        public void TestSplitAndSort()
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(20);
            df.Columns["Int"][0] = 100000;
            df.Columns["Int"][df.Rows.Count - 1] = -1;
            df.Columns["Int"][5] = 200000;
            DataFrame dfTest;
            DataFrame dfTrain = SplitTrainTest(df, 0.8f, out dfTest);

            // Sort by "Int" in ascending order
            var sortedDf = dfTrain.OrderBy("Int");
            Assert.Null(sortedDf.Columns["Int"][sortedDf.Rows.Count - 1]);
            Assert.Equal(1, sortedDf.Columns["Int"][0]);
            Assert.Equal(100000, sortedDf.Columns["Int"][sortedDf.Rows.Count - 3]);
            Assert.Equal(200000, sortedDf.Columns["Int"][sortedDf.Rows.Count - 2]);
        }

        [Fact]
        public void TestStringColumnSort()
        {
            // StringDataFrameColumn specific sort tests
            StringDataFrameColumn strColumn = new StringDataFrameColumn("String", 0);
            Assert.Equal(0, strColumn.NullCount);
            for (int i = 0; i < 5; i++)
            {
                strColumn.Append(null);
            }
            Assert.Equal(5, strColumn.NullCount);
            // Should handle all nulls
            StringDataFrameColumn sortedStrColumn = strColumn.Sort() as StringDataFrameColumn;
            Assert.Equal(5, sortedStrColumn.NullCount);
            Assert.Null(sortedStrColumn[0]);

            for (int i = 0; i < 5; i++)
            {
                strColumn.Append(i.ToString());
            }
            Assert.Equal(5, strColumn.NullCount);

            // Ascending sort
            sortedStrColumn = strColumn.Sort() as StringDataFrameColumn;
            Assert.Equal("0", sortedStrColumn[0]);
            Assert.Null(sortedStrColumn[9]);

            // Descending sort
            sortedStrColumn = strColumn.Sort(false) as StringDataFrameColumn;
            Assert.Equal("4", sortedStrColumn[0]);
            Assert.Null(sortedStrColumn[9]);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(12)]
        [InlineData(100)]
        [InlineData(1000)]
        public void TestPrimitiveColumnSort(int numberOfNulls)
        {
            // Primitive Column Sort
            Int32DataFrameColumn intColumn = new Int32DataFrameColumn("Int", 0);
            Assert.Equal(0, intColumn.NullCount);
            intColumn.AppendMany(null, numberOfNulls);
            Assert.Equal(numberOfNulls, intColumn.NullCount);

            // Should handle all nulls
            PrimitiveDataFrameColumn<int> sortedIntColumn = intColumn.Sort();
            Assert.Equal(numberOfNulls, sortedIntColumn.NullCount);
            Assert.Null(sortedIntColumn[0]);

            for (int i = 0; i < 5; i++)
            {
                intColumn.Append(i);
            }
            Assert.Equal(numberOfNulls, intColumn.NullCount);

            // Ascending sort
            sortedIntColumn = intColumn.Sort();
            Assert.Equal(0, sortedIntColumn[0]);
            Assert.Null(sortedIntColumn[9]);

            // Descending sort
            sortedIntColumn = intColumn.Sort(ascending: false);
            Assert.Equal(4, sortedIntColumn[0]);
            Assert.Null(sortedIntColumn[9]);
        }

        [Fact]
        public void TestSortWithDifferentNullCountsInColumns()
        {
            DataFrame dataFrame = MakeDataFrameWithAllMutableColumnTypes(10);
            dataFrame["Int"][3] = null;
            dataFrame["String"][3] = null;
            DataFrame sorted = dataFrame.OrderBy("Int");
            void Verify(DataFrame sortedDataFrame)
            {
                Assert.Equal(10, sortedDataFrame.Rows.Count);
                DataFrameRow lastRow = sortedDataFrame.Rows[sortedDataFrame.Rows.Count - 1];
                DataFrameRow penultimateRow = sortedDataFrame.Rows[sortedDataFrame.Rows.Count - 2];
                foreach (object value in lastRow)
                {
                    Assert.Null(value);
                }

                for (int i = 0; i < sortedDataFrame.Columns.Count; i++)
                {
                    string columnName = sortedDataFrame.Columns[i].Name;
                    if (columnName != "String" && columnName != "Int")
                    {
                        Assert.Equal(dataFrame[columnName][3], penultimateRow[i]);
                    }
                    else if (columnName == "String" || columnName == "Int")
                    {
                        Assert.Null(penultimateRow[i]);
                    }
                }
            }

            Verify(sorted);

            sorted = dataFrame.OrderBy("String");
            Verify(sorted);
        }

        private void VerifyJoin(DataFrame join, DataFrame left, DataFrame right, JoinAlgorithm joinAlgorithm)
        {
            Int64DataFrameColumn mapIndices = new Int64DataFrameColumn("map", join.Rows.Count);
            for (long i = 0; i < join.Rows.Count; i++)
            {
                mapIndices[i] = i;
            }
            for (int i = 0; i < join.Columns.Count; i++)
            {
                DataFrameColumn joinColumn = join.Columns[i];
                DataFrameColumn isEqual;

                if (joinAlgorithm == JoinAlgorithm.Left)
                {
                    if (i < left.Columns.Count)
                    {
                        DataFrameColumn leftColumn = left.Columns[i];
                        isEqual = joinColumn.ElementwiseEquals(leftColumn);
                    }
                    else
                    {
                        int columnIndex = i - left.Columns.Count;
                        DataFrameColumn rightColumn = right.Columns[columnIndex];
                        DataFrameColumn compareColumn = rightColumn.Length <= join.Rows.Count ? rightColumn.Clone(numberOfNullsToAppend: join.Rows.Count - rightColumn.Length) : rightColumn.Clone(mapIndices);
                        isEqual = joinColumn.ElementwiseEquals(compareColumn);
                    }
                }
                else if (joinAlgorithm == JoinAlgorithm.Right)
                {
                    if (i < left.Columns.Count)
                    {
                        DataFrameColumn leftColumn = left.Columns[i];
                        DataFrameColumn compareColumn = leftColumn.Length <= join.Rows.Count ? leftColumn.Clone(numberOfNullsToAppend: join.Rows.Count - leftColumn.Length) : leftColumn.Clone(mapIndices);
                        isEqual = joinColumn.ElementwiseEquals(compareColumn);
                    }
                    else
                    {
                        int columnIndex = i - left.Columns.Count;
                        DataFrameColumn rightColumn = right.Columns[columnIndex];
                        isEqual = joinColumn.ElementwiseEquals(rightColumn);
                    }
                }
                else if (joinAlgorithm == JoinAlgorithm.Inner)
                {
                    if (i < left.Columns.Count)
                    {
                        DataFrameColumn leftColumn = left.Columns[i];
                        isEqual = joinColumn.ElementwiseEquals(leftColumn.Clone(mapIndices));
                    }
                    else
                    {
                        int columnIndex = i - left.Columns.Count;
                        DataFrameColumn rightColumn = right.Columns[columnIndex];
                        isEqual = joinColumn.ElementwiseEquals(rightColumn.Clone(mapIndices));
                    }
                }
                else
                {
                    if (i < left.Columns.Count)
                    {
                        DataFrameColumn leftColumn = left.Columns[i];
                        isEqual = joinColumn.ElementwiseEquals(leftColumn.Clone(numberOfNullsToAppend: join.Rows.Count - leftColumn.Length));
                    }
                    else
                    {
                        int columnIndex = i - left.Columns.Count;
                        DataFrameColumn rightColumn = right.Columns[columnIndex];
                        isEqual = joinColumn.ElementwiseEquals(rightColumn.Clone(numberOfNullsToAppend: join.Rows.Count - rightColumn.Length));
                    }
                }
                for (int j = 0; j < join.Rows.Count; j++)
                {
                    Assert.Equal(true, isEqual[j]);
                }
            }
        }

        private void VerifyMerge(DataFrame merge, DataFrame left, DataFrame right, JoinAlgorithm joinAlgorithm)
        {
            if (joinAlgorithm == JoinAlgorithm.Left || joinAlgorithm == JoinAlgorithm.Inner)
            {
                HashSet<int> intersection = new HashSet<int>();
                for (int i = 0; i < merge.Columns["Int_left"].Length; i++)
                {
                    if (merge.Columns["Int_left"][i] == null)
                        continue;
                    intersection.Add((int)merge.Columns["Int_left"][i]);
                }
                for (int i = 0; i < left.Columns["Int"].Length; i++)
                {
                    if (left.Columns["Int"][i] != null && intersection.Contains((int)left.Columns["Int"][i]))
                        intersection.Remove((int)left.Columns["Int"][i]);
                }
                Assert.Empty(intersection);
            }
            else if (joinAlgorithm == JoinAlgorithm.Right)
            {
                HashSet<int> intersection = new HashSet<int>();
                for (int i = 0; i < merge.Columns["Int_right"].Length; i++)
                {
                    if (merge.Columns["Int_right"][i] == null)
                        continue;
                    intersection.Add((int)merge.Columns["Int_right"][i]);
                }
                for (int i = 0; i < right.Columns["Int"].Length; i++)
                {
                    if (right.Columns["Int"][i] != null && intersection.Contains((int)right.Columns["Int"][i]))
                        intersection.Remove((int)right.Columns["Int"][i]);
                }
                Assert.Empty(intersection);
            }
            else if (joinAlgorithm == JoinAlgorithm.FullOuter)
            {
                VerifyMerge(merge, left, right, JoinAlgorithm.Left);
                VerifyMerge(merge, left, right, JoinAlgorithm.Right);
            }
        }

        [Fact]
        public void TestJoin()
        {
            DataFrame left = MakeDataFrameWithAllMutableColumnTypes(10);
            DataFrame right = MakeDataFrameWithAllMutableColumnTypes(5);

            // Tests with right.Rows.Count < left.Rows.Count
            // Left join
            DataFrame join = left.Join(right);
            Assert.Equal(join.Rows.Count, left.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Null(join.Columns["Int_right"][6]);
            VerifyJoin(join, left, right, JoinAlgorithm.Left);

            // Right join
            join = left.Join(right, joinAlgorithm: JoinAlgorithm.Right);
            Assert.Equal(join.Rows.Count, right.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(join.Columns["Int_right"][3], right.Columns["Int"][3]);
            Assert.Null(join.Columns["Int_right"][2]);
            VerifyJoin(join, left, right, JoinAlgorithm.Right);

            // Outer join
            join = left.Join(right, joinAlgorithm: JoinAlgorithm.FullOuter);
            Assert.Equal(join.Rows.Count, left.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Null(join.Columns["Int_right"][6]);
            VerifyJoin(join, left, right, JoinAlgorithm.FullOuter);

            // Inner join
            join = left.Join(right, joinAlgorithm: JoinAlgorithm.Inner);
            Assert.Equal(join.Rows.Count, right.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(join.Columns["Int_right"][3], right.Columns["Int"][3]);
            Assert.Null(join.Columns["Int_right"][2]);
            VerifyJoin(join, left, right, JoinAlgorithm.Inner);

            // Tests with right.Rows.Count > left.Rows.Count
            // Left join
            right = MakeDataFrameWithAllMutableColumnTypes(15);
            join = left.Join(right);
            Assert.Equal(join.Rows.Count, left.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(join.Columns["Int_right"][6], right.Columns["Int"][6]);
            VerifyJoin(join, left, right, JoinAlgorithm.Left);

            // Right join
            join = left.Join(right, joinAlgorithm: JoinAlgorithm.Right);
            Assert.Equal(join.Rows.Count, right.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(join.Columns["Int_right"][2], right.Columns["Int"][2]);
            Assert.Null(join.Columns["Int_left"][12]);
            VerifyJoin(join, left, right, JoinAlgorithm.Right);

            // Outer join
            join = left.Join(right, joinAlgorithm: JoinAlgorithm.FullOuter);
            Assert.Equal(join.Rows.Count, right.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Null(join.Columns["Int_left"][12]);
            VerifyJoin(join, left, right, JoinAlgorithm.FullOuter);

            // Inner join
            join = left.Join(right, joinAlgorithm: JoinAlgorithm.Inner);
            Assert.Equal(join.Rows.Count, left.Rows.Count);
            Assert.Equal(join.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(join.Columns["Int_right"][2], right.Columns["Int"][2]);
            VerifyJoin(join, left, right, JoinAlgorithm.Inner);
        }

        [Fact]
        public void TestGroupBy()
        {
            DataFrame df = MakeDataFrameWithNumericAndBoolColumns(10);
            DataFrame count = df.GroupBy("Bool").Count();
            Assert.Equal(2, count.Rows.Count);
            Assert.Equal((long)5, count.Columns["Int"][0]);
            Assert.Equal((long)4, count.Columns["Decimal"][1]);
            for (int r = 0; r < count.Rows.Count; r++)
            {
                for (int c = 1; c < count.Columns.Count; c++)
                {
                    Assert.Equal((long)(r == 0 ? 5 : 4), count.Columns[c][r]);
                }
            }

            DataFrame first = df.GroupBy("Bool").First();
            Assert.Equal(2, first.Rows.Count);
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < count.Columns.Count; c++)
                {
                    DataFrameColumn originalColumn = df.Columns[c];
                    DataFrameColumn firstColumn = first.Columns[originalColumn.Name];
                    Assert.Equal(originalColumn[r], firstColumn[r]);
                }
            }

            DataFrame head = df.GroupBy("Bool").Head(3);
            List<int> verify = new List<int>() { 0, 3, 1, 4, 2, 5 };
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < count.Columns.Count; c++)
                {
                    DataFrameColumn originalColumn = df.Columns[c];
                    DataFrameColumn headColumn = head.Columns[originalColumn.Name];
                    Assert.Equal(originalColumn[r].ToString(), headColumn[verify[r]].ToString());
                }
            }
            for (int c = 0; c < count.Columns.Count; c++)
            {
                DataFrameColumn originalColumn = df.Columns[c];
                if (originalColumn.Name == "Bool")
                    continue;
                DataFrameColumn headColumn = head.Columns[originalColumn.Name];
                Assert.Equal(originalColumn[7], headColumn[verify[5]]);
            }
            Assert.Equal(6, head.Rows.Count);

            DataFrame tail = df.GroupBy("Bool").Tail(3);
            Assert.Equal(6, tail.Rows.Count);
            List<int> originalColumnVerify = new List<int>() { 6, 8, 7, 9 };
            List<int> tailColumnVerity = new List<int>() { 1, 2, 4, 5 };
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < count.Columns.Count; c++)
                {
                    DataFrameColumn originalColumn = df.Columns[c];
                    DataFrameColumn tailColumn = tail.Columns[originalColumn.Name];
                    Assert.Equal(originalColumn[originalColumnVerify[r]].ToString(), tailColumn[tailColumnVerity[r]].ToString());
                }
            }

            DataFrame max = df.GroupBy("Bool").Max();
            Assert.Equal(2, max.Rows.Count);
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < count.Columns.Count; c++)
                {
                    DataFrameColumn originalColumn = df.Columns[c];
                    if (originalColumn.Name == "Bool" || originalColumn.Name == "Char")
                        continue;
                    DataFrameColumn maxColumn = max.Columns[originalColumn.Name];
                    Assert.Equal(((long)(r == 0 ? 8 : 9)).ToString(), maxColumn[r].ToString());
                }
            }

            DataFrame min = df.GroupBy("Bool").Min();
            Assert.Equal(2, min.Rows.Count);

            DataFrame product = df.GroupBy("Bool").Product();
            Assert.Equal(2, product.Rows.Count);

            DataFrame sum = df.GroupBy("Bool").Sum();
            Assert.Equal(2, sum.Rows.Count);

            DataFrame mean = df.GroupBy("Bool").Mean();
            Assert.Equal(2, mean.Rows.Count);
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < count.Columns.Count; c++)
                {
                    DataFrameColumn originalColumn = df.Columns[c];
                    if (originalColumn.Name == "Bool" || originalColumn.Name == "Char")
                        continue;
                    DataFrameColumn minColumn = min.Columns[originalColumn.Name];
                    Assert.Equal(r == 0 ? "0" : "1", minColumn[r].ToString());

                    DataFrameColumn productColumn = product.Columns[originalColumn.Name];
                    Assert.Equal("0", productColumn[r].ToString());

                    DataFrameColumn sumColumn = sum.Columns[originalColumn.Name];
                    Assert.Equal("20", sumColumn[r].ToString());
                }
            }

            DataFrame columnSum = df.GroupBy("Bool").Sum("Int");
            Assert.Equal(2, columnSum.Columns.Count);
            Assert.Equal(20, columnSum.Columns["Int"][0]);
            Assert.Equal(20, columnSum.Columns["Int"][1]);
            DataFrame columnMax = df.GroupBy("Bool").Max("Int");
            Assert.Equal(2, columnMax.Columns.Count);
            Assert.Equal(8, columnMax.Columns["Int"][0]);
            Assert.Equal(9, columnMax.Columns["Int"][1]);
            DataFrame columnProduct = df.GroupBy("Bool").Product("Int");
            Assert.Equal(2, columnProduct.Columns.Count);
            Assert.Equal(0, columnProduct.Columns["Int"][0]);
            Assert.Equal(0, columnProduct.Columns["Int"][1]);
            DataFrame columnMin = df.GroupBy("Bool").Min("Int");
            Assert.Equal(2, columnMin.Columns.Count);
            Assert.Equal(0, columnMin.Columns["Int"][0]);
            Assert.Equal(1, columnMin.Columns["Int"][1]);

            DataFrame countIntColumn = df.GroupBy("Bool").Count("Int");
            Assert.Equal(2, countIntColumn.Columns.Count);
            Assert.Equal(2, countIntColumn.Rows.Count);
            Assert.Equal((long)5, countIntColumn.Columns["Int"][0]);
            Assert.Equal((long)4, countIntColumn.Columns["Int"][1]);

            DataFrame firstDecimalColumn = df.GroupBy("Bool").First("Decimal");
            Assert.Equal(2, firstDecimalColumn.Columns.Count);
            Assert.Equal(2, firstDecimalColumn.Rows.Count);
            Assert.Equal((decimal)0, firstDecimalColumn.Columns["Decimal"][0]);
            Assert.Equal((decimal)1, firstDecimalColumn.Columns["Decimal"][1]);
        }

        [Fact]
        public void TestGroupByDifferentColumnTypes()
        {
            void GroupCountAndAssert(DataFrame frame)
            {
                DataFrame grouped = frame.GroupBy("Column1").Count();
                Assert.Equal(2, grouped.Rows.Count);
            }

            DataFrame df = MakeDataFrame<byte, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<char, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<decimal, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<double, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<float, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<int, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<long, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<sbyte, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<short, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<uint, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<ulong, bool>(10, false);
            GroupCountAndAssert(df);

            df = MakeDataFrame<ushort, bool>(10, false);
            GroupCountAndAssert(df);
        }

        [Fact]
        public void TestIEnumerable()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10);

            int totalValueCount = 0;
            for (int i = 0; i < df.Columns.Count; i++)
            {
                DataFrameColumn baseColumn = df.Columns[i];
                foreach (object value in baseColumn)
                {
                    totalValueCount++;
                }
            }
            Assert.Equal(10 * df.Columns.Count, totalValueCount);

            // spot check a few column types:

            StringDataFrameColumn stringColumn = (StringDataFrameColumn)df.Columns["String"];
            StringBuilder actualStrings = new StringBuilder();
            foreach (string value in stringColumn)
            {
                if (value == null)
                {
                    actualStrings.Append("<null>");
                }
                else
                {
                    actualStrings.Append(value);
                }
            }
            Assert.Equal("01234<null>6789", actualStrings.ToString());

            ArrowStringDataFrameColumn arrowStringColumn = (ArrowStringDataFrameColumn)df.Columns["ArrowString"];
            actualStrings.Clear();
            foreach (string value in arrowStringColumn)
            {
                if (value == null)
                {
                    actualStrings.Append("<null>");
                }
                else
                {
                    actualStrings.Append(value);
                }
            }
            Assert.Equal("foofoofoofoofoo<null>foofoofoofoo", actualStrings.ToString());

            SingleDataFrameColumn floatColumn = (SingleDataFrameColumn)df.Columns["Float"];
            actualStrings.Clear();
            foreach (float? value in floatColumn)
            {
                if (value == null)
                {
                    actualStrings.Append("<null>");
                }
                else
                {
                    actualStrings.Append(value);
                }
            }
            Assert.Equal("01234<null>6789", actualStrings.ToString());

            Int32DataFrameColumn intColumn = (Int32DataFrameColumn)df.Columns["Int"];
            actualStrings.Clear();
            foreach (int? value in intColumn)
            {
                if (value == null)
                {
                    actualStrings.Append("<null>");
                }
                else
                {
                    actualStrings.Append(value);
                }
            }
            Assert.Equal("01234<null>6789", actualStrings.ToString());
        }

        [Fact]
        public void TestColumnClamp()
        {
            DataFrame df = MakeDataFrameWithNumericColumns(10);
            // Out of place
            DataFrameColumn clamped = df.Columns["Int"].Clamp(3, 7);
            Assert.Equal(3, clamped[0]);
            Assert.Equal(0, df.Columns["Int"][0]);
            Assert.Equal(3, clamped[1]);
            Assert.Equal(1, df.Columns["Int"][1]);
            Assert.Equal(3, clamped[2]);
            Assert.Equal(2, df.Columns["Int"][2]);
            Assert.Equal(3, clamped[3]);
            Assert.Equal(3, df.Columns["Int"][3]);
            Assert.Equal(4, clamped[4]);
            Assert.Equal(4, df.Columns["Int"][4]);
            Assert.Null(clamped[5]);
            Assert.Null(df.Columns["Int"][5]);
            Assert.Equal(6, clamped[6]);
            Assert.Equal(6, df.Columns["Int"][6]);
            Assert.Equal(7, clamped[7]);
            Assert.Equal(7, df.Columns["Int"][7]);
            Assert.Equal(7, clamped[8]);
            Assert.Equal(8, df.Columns["Int"][8]);
            Assert.Equal(7, clamped[9]);
            Assert.Equal(9, df.Columns["Int"][9]);

            // In place
            df.Columns["Int"].Clamp(3, 7, true);
            Assert.Equal(3, df.Columns["Int"][0]);
            Assert.Equal(3, df.Columns["Int"][1]);
            Assert.Equal(3, df.Columns["Int"][2]);
            Assert.Equal(3, df.Columns["Int"][3]);
            Assert.Equal(4, df.Columns["Int"][4]);
            Assert.Null(df.Columns["Int"][5]);
            Assert.Equal(6, df.Columns["Int"][6]);
            Assert.Equal(7, df.Columns["Int"][7]);
            Assert.Equal(7, df.Columns["Int"][8]);
            Assert.Equal(7, df.Columns["Int"][9]);
        }

        [Fact]
        public void TestColumnFilter()
        {
            DataFrame df = MakeDataFrameWithNumericColumns(10);
            DataFrameColumn filtered = df.Columns["Int"].Filter(3, 7);
            Assert.Equal(4, filtered.Length);
            Assert.Equal(3, filtered[0]);
            Assert.Equal(4, filtered[1]);
            Assert.Equal(6, filtered[2]);
            Assert.Equal(7, filtered[3]);
        }

        [Fact]
        public void TestDataFrameClamp()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10);
            IEnumerable<DataViewSchema.Column> dfColumns = ((IDataView)df).Schema;

            void VerifyDataFrameClamp(DataFrame clampedColumn)
            {

                IEnumerable<DataViewSchema.Column> clampedColumns = ((IDataView)clampedColumn).Schema;
                Assert.Equal(df.Columns.Count, clampedColumn.Columns.Count);
                Assert.Equal(dfColumns, clampedColumns);
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    DataFrameColumn column = clampedColumn.Columns[c];
                    if (column.IsNumericColumn())
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            Assert.Equal("3", column[i].ToString());
                        }
                        Assert.Equal(4.ToString(), column[4].ToString());
                        Assert.Null(column[5]);
                        Assert.Equal(6.ToString(), column[6].ToString());
                        for (int i = 7; i < 10; i++)
                        {
                            Assert.Equal("7", column[i].ToString());
                        }
                    }
                    else
                    {
                        for (int i = 0; i < column.Length; i++)
                        {
                            var colD = df.Columns[c][i];
                            var ocD = column[i];
                            Assert.Equal(df.Columns[c][i], column[i]);
                        }
                    }
                }
            }

            // Out of place
            DataFrame clamped = df.Clamp(3, 7);
            VerifyDataFrameClamp(clamped);
            for (int i = 0; i < 10; i++)
            {
                if (i != 5)
                    Assert.Equal(i, df.Columns["Int"][i]);
                else
                    Assert.Null(df.Columns["Int"][5]);
            }

            // Inplace
            df.Clamp(3, 7, true);
            VerifyDataFrameClamp(df);

        }

        [Fact]
        public void TestDataFrameFilter()
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(10);
            DataFrame boolColumnFiltered = df[df.Columns["Bool"].ElementwiseEquals(true)];
            List<int> verify = new List<int> { 0, 2, 4, 6, 8 };
            Assert.Equal(5, boolColumnFiltered.Rows.Count);
            for (int i = 0; i < boolColumnFiltered.Columns.Count; i++)
            {
                DataFrameColumn column = boolColumnFiltered.Columns[i];
                if (column.Name == "Char" || column.Name == "Bool" || column.Name == "String" || column.Name == "DateTime")
                    continue;
                for (int j = 0; j < column.Length; j++)
                {
                    Assert.Equal(verify[j].ToString(), column[j].ToString());
                }
            }
            DataFrame intEnumerableFiltered = df[Enumerable.Range(0, 10)];
            DataFrame boolEnumerableFiltered = df[Enumerable.Range(0, 10).Select(x => true)];
            DataFrame longEnumerableFiltered = df[Enumerable.Range(0, 10).Select(x => (long)x)];
            Assert.Equal(intEnumerableFiltered.Columns.Count, df.Columns.Count);
            Assert.Equal(boolEnumerableFiltered.Columns.Count, df.Columns.Count);
            Assert.Equal(longEnumerableFiltered.Columns.Count, df.Columns.Count);
            for (int i = 0; i < intEnumerableFiltered.Columns.Count; i++)
            {
                DataFrameColumn intFilteredColumn = intEnumerableFiltered.Columns[i];
                DataFrameColumn dfColumn = df.Columns[i];
                DataFrameColumn boolFilteredColumn = boolEnumerableFiltered.Columns[i];
                DataFrameColumn longFilteredColumn = longEnumerableFiltered.Columns[i];
                Assert.True(intFilteredColumn.ElementwiseEquals(dfColumn).All());
                Assert.True(boolFilteredColumn.ElementwiseEquals(dfColumn).All());
                Assert.True(longFilteredColumn.ElementwiseEquals(dfColumn).All());
            }
        }

        [Fact]
        public void TestPrefixAndSuffix()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10);
            IEnumerable<DataViewSchema.Column> columnNames = ((IDataView)df).Schema;

            DataFrame prefix = df.AddPrefix("Prefix_");
            IEnumerable<DataViewSchema.Column> prefixNames = ((IDataView)prefix).Schema;
            foreach ((DataViewSchema.Column First, DataViewSchema.Column Second) in columnNames.Zip(((IDataView)df).Schema, (e1, e2) => (e1, e2)))
            {
                Assert.Equal(First.Name, Second.Name);
            }
            foreach ((DataViewSchema.Column First, DataViewSchema.Column Second) in prefixNames.Zip(columnNames, (e1, e2) => (e1, e2)))
            {
                Assert.Equal(First.Name, "Prefix_" + Second.Name);
            }

            // Inplace
            df.AddPrefix("Prefix_", true);
            prefixNames = ((IDataView)df).Schema;
            foreach ((DataViewSchema.Column First, DataViewSchema.Column Second) in columnNames.Zip(prefixNames, (e1, e2) => (e1, e2)))
            {
                Assert.Equal("Prefix_" + First.Name, Second.Name);
            }

            DataFrame suffix = df.AddSuffix("_Suffix");
            IEnumerable<DataViewSchema.Column> suffixNames = ((IDataView)suffix).Schema;
            foreach ((DataViewSchema.Column First, DataViewSchema.Column Second) in ((IDataView)df).Schema.Zip(columnNames, (e1, e2) => (e1, e2)))
            {
                Assert.Equal(First.Name, "Prefix_" + Second.Name);
            }
            foreach ((DataViewSchema.Column First, DataViewSchema.Column Second) in columnNames.Zip(suffixNames, (e1, e2) => (e1, e2)))
            {
                Assert.Equal("Prefix_" + First.Name + "_Suffix", Second.Name);
            }

            // InPlace
            df.AddSuffix("_Suffix", true);
            suffixNames = ((IDataView)df).Schema;
            foreach ((DataViewSchema.Column First, DataViewSchema.Column Second) in columnNames.Zip(suffixNames, (e1, e2) => (e1, e2)))
            {
                Assert.Equal("Prefix_" + First.Name + "_Suffix", Second.Name);
            }
        }

        [Fact]
        public void TestSample()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10);
            DataFrame sampled = df.Sample(7);
            Assert.Equal(7, sampled.Rows.Count);
            Assert.Equal(df.Columns.Count, sampled.Columns.Count);

            // all sampled rows should be unique.
            HashSet<int?> uniqueRowValues = new HashSet<int?>();
            foreach (int? value in sampled.Columns["Int"])
            {
                uniqueRowValues.Add(value);
            }
            Assert.Equal(uniqueRowValues.Count, sampled.Rows.Count);

            // should throw exception as sample size is greater than dataframe rows
            Assert.Throws<ArgumentException>(() => df.Sample(13));
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 1)]
        public void TestDataCorrectnessForInnerMerge(int leftCount, int rightCount)
        {
            DataFrame left = MakeDataFrameWithNumericColumns(leftCount, false);
            DataFrameColumn leftStringColumn = new StringDataFrameColumn("String", Enumerable.Range(0, leftCount).Select(x => "Left"));
            left.Columns.Insert(left.Columns.Count, leftStringColumn);

            DataFrame right = MakeDataFrameWithNumericColumns(rightCount, false);
            DataFrameColumn rightStringColumn = new StringDataFrameColumn("String", Enumerable.Range(0, rightCount).Select(x => "Right"));
            right.Columns.Insert(right.Columns.Count, rightStringColumn);

            DataFrame merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.Inner);

            Assert.Equal("Left", (string)merge.Columns["String_left"][0]);
            Assert.Equal("Right", (string)merge.Columns["String_right"][0]);
        }

        [Fact]
        public void TestMerge()
        {
            DataFrame left = MakeDataFrameWithAllMutableColumnTypes(10);
            DataFrame right = MakeDataFrameWithAllMutableColumnTypes(5);

            // Tests with right.Rows.Count < left.Rows.Count
            // Left merge
            DataFrame merge = left.Merge<int>(right, "Int", "Int");
            Assert.Equal(10, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Null(merge.Columns["Int_right"][6]);
            Assert.Null(merge.Columns["Int_left"][5]);
            VerifyMerge(merge, left, right, JoinAlgorithm.Left);

            // Right merge
            merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.Right);
            Assert.Equal(5, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(merge.Columns["Int_right"][3], right.Columns["Int"][3]);
            Assert.Null(merge.Columns["Int_right"][2]);
            VerifyMerge(merge, left, right, JoinAlgorithm.Right);

            // Outer merge
            merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.FullOuter);
            Assert.Equal(merge.Rows.Count, left.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Null(merge.Columns["Int_right"][6]);
            VerifyMerge(merge, left, right, JoinAlgorithm.FullOuter);

            // Inner merge
            merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.Inner);
            Assert.Equal(merge.Rows.Count, right.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(merge.Columns["Int_right"][2], right.Columns["Int"][3]);
            Assert.Null(merge.Columns["Int_right"][4]);
            VerifyMerge(merge, left, right, JoinAlgorithm.Inner);

            // Tests with right.Rows.Count > left.Rows.Count
            // Left merge
            right = MakeDataFrameWithAllMutableColumnTypes(15);
            merge = left.Merge<int>(right, "Int", "Int");
            Assert.Equal(merge.Rows.Count, left.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(merge.Columns["Int_right"][6], right.Columns["Int"][6]);
            VerifyMerge(merge, left, right, JoinAlgorithm.Left);

            // Right merge
            merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.Right);
            Assert.Equal(merge.Rows.Count, right.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(merge.Columns["Int_right"][2], right.Columns["Int"][2]);
            Assert.Null(merge.Columns["Int_left"][12]);
            VerifyMerge(merge, left, right, JoinAlgorithm.Right);

            // Outer merge
            merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.FullOuter);
            Assert.Equal(16, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Null(merge.Columns["Int_left"][12]);
            Assert.Null(merge.Columns["Int_left"][15]);
            VerifyMerge(merge, left, right, JoinAlgorithm.FullOuter);

            // Inner merge
            merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.Inner);
            Assert.Equal(9, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            Assert.Equal(merge.Columns["Int_right"][2], right.Columns["Int"][2]);
            VerifyMerge(merge, left, right, JoinAlgorithm.Inner);
        }

        private void MatchRowsOnMergedDataFrame(DataFrame merge, DataFrame left, DataFrame right, long mergeRow, long? leftRow, long? rightRow)
        {
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            DataFrameRow dataFrameMergeRow = merge.Rows[mergeRow];
            int columnIndex = 0;
            foreach (object value in dataFrameMergeRow)
            {
                object compare = null;
                if (columnIndex < left.Columns.Count)
                {
                    if (leftRow != null)
                    {
                        compare = left.Rows[leftRow.Value][columnIndex];
                    }
                }
                else
                {
                    int rightColumnIndex = columnIndex - left.Columns.Count;
                    if (rightRow != null)
                    {
                        compare = right.Rows[rightRow.Value][rightColumnIndex];
                    }
                }
                Assert.Equal(value, compare);
                columnIndex++;
            }
        }

        [Theory]
        [InlineData(10, 5, JoinAlgorithm.Left)]
        [InlineData(5, 10, JoinAlgorithm.Right)]
        public void TestMergeEdgeCases_LeftOrRight(int leftLength, int rightLength, JoinAlgorithm joinAlgorithm)
        {
            DataFrame left = MakeDataFrameWithAllMutableColumnTypes(leftLength);
            if (leftLength > 5)
            {
                left["Int"][8] = null;
            }
            DataFrame right = MakeDataFrameWithAllMutableColumnTypes(rightLength);
            if (rightLength > 5)
            {
                right["Int"][8] = null;
            }

            DataFrame merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: joinAlgorithm);
            Assert.Equal(10, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);
            int[] matchedFullRows = new int[] { 0, 1, 3, 4 };
            for (long i = 0; i < matchedFullRows.Length; i++)
            {
                int rowIndex = matchedFullRows[i];
                MatchRowsOnMergedDataFrame(merge, left, right, rowIndex, rowIndex, rowIndex);
            }

            int[] matchedLeftOrRightRowsNullOtherRows = new int[] { 2, 5, 6, 7, 8, 9 };
            for (long i = 0; i < matchedLeftOrRightRowsNullOtherRows.Length; i++)
            {
                int rowIndex = matchedLeftOrRightRowsNullOtherRows[i];
                if (leftLength > 5)
                {
                    MatchRowsOnMergedDataFrame(merge, left, right, rowIndex, rowIndex, null);
                }
                else
                {
                    MatchRowsOnMergedDataFrame(merge, left, right, rowIndex, null, rowIndex);
                }
            }
        }

        [Fact]
        public void TestMergeEdgeCases_Inner()
        {
            DataFrame left = MakeDataFrameWithAllMutableColumnTypes(5);
            DataFrame right = MakeDataFrameWithAllMutableColumnTypes(10);
            left["Int"][3] = null;
            right["Int"][6] = null;
            // Creates this case:
            /*
             * Left:    Right:
             * 0        0
             * 1        1
             * null(2)  2
             * null(3)  3
             * 4        4
             *          null(5)
             *          null(6)
             *          7
             *          8
             *          9
             */
            /*
             * Merge will result in a DataFrame like:
             * Int_Left Int_Right
             * 0        0
             * 1        1
             * 4        4
             * null(2)  null(5)
             * null(3)  null(5)
             * null(2)  null(6)
             * null(3)  null(6)
             */

            DataFrame merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.Inner);
            Assert.Equal(7, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            int[] mergeRows = new int[] { 0, 1, 2, 3, 4, 5, 6 };
            int[] leftRows = new int[] { 0, 1, 4, 2, 3, 2, 3 };
            int[] rightRows = new int[] { 0, 1, 4, 5, 5, 6, 6 };
            for (long i = 0; i < mergeRows.Length; i++)
            {
                int rowIndex = mergeRows[i];
                int leftRowIndex = leftRows[i];
                int rightRowIndex = rightRows[i];
                MatchRowsOnMergedDataFrame(merge, left, right, rowIndex, leftRowIndex, rightRowIndex);
            }
        }

        [Fact]
        public void TestMergeEdgeCases_Outer()
        {
            DataFrame left = MakeDataFrameWithAllMutableColumnTypes(5);
            left["Int"][3] = null;
            DataFrame right = MakeDataFrameWithAllMutableColumnTypes(5);
            right["Int"][1] = 5;
            right["Int"][3] = null;
            right["Int"][4] = 6;

            // Creates this case:
            /*
             * Left:    Right:    RowIndex:
             * 0        0         0
             * 1        5         1
             * null     null      2
             * null(3)  null(3)   3
             * 4        6         4
             */

            /*
             * Merge will result in a DataFrame like:
             * Int_left:    Int_right:        Merged:    Index:
             * 0            0                 0 - 0      0
             * 1            null              1 - N      1
             * null         null              2 - 2      2
             * null         null(3)           2 - 3      3
             * null(3)      null              3 - 2      4
             * null(3)      null(3)           3 - 3      5
             * 4            null              4 - N      6
             * null         5                 N - 1      7
             * null         6                 N - 4      8
             */

            DataFrame merge = left.Merge<int>(right, "Int", "Int", joinAlgorithm: JoinAlgorithm.FullOuter);
            Assert.Equal(9, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            int[] mergeRows = new int[] { 0, 2, 3, 4, 5 };
            int[] leftRows = new int[] { 0, 2, 2, 3, 3 };
            int[] rightRows = new int[] { 0, 2, 3, 2, 3 };
            for (long i = 0; i < mergeRows.Length; i++)
            {
                int rowIndex = mergeRows[i];
                int leftRowIndex = leftRows[i];
                int rightRowIndex = rightRows[i];
                MatchRowsOnMergedDataFrame(merge, left, right, rowIndex, leftRowIndex, rightRowIndex);
            }

            mergeRows = new int[] { 1, 6 };
            leftRows = new int[] { 1, 4 };
            for (long i = 0; i < mergeRows.Length; i++)
            {
                int rowIndex = mergeRows[i];
                int leftRowIndex = leftRows[i];
                MatchRowsOnMergedDataFrame(merge, left, right, rowIndex, leftRowIndex, null);
            }

            mergeRows = new int[] { 7, 8 };
            rightRows = new int[] { 1, 4 };
            for (long i = 0; i < mergeRows.Length; i++)
            {
                int rowIndex = mergeRows[i];
                int rightRowIndex = rightRows[i];
                MatchRowsOnMergedDataFrame(merge, left, right, rowIndex, null, rightRowIndex);
            }
        }

        [Fact]
        public void TestMerge_ByTwoColumns_Complex_LeftJoin()
        {
            //Test left merge by to int type columns

            //Arrange
            var left = new DataFrame();
            left.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2, 3, 4, 5 }));
            left.Columns.Add(new Int32DataFrameColumn("G1", new[] { 0, 1, 1, 2, 2, 3 }));
            left.Columns.Add(new Int32DataFrameColumn("G2", new[] { 3, 1, 2, 1, 2, 1 }));

            var right = new DataFrame();
            right.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2, 3 }));
            right.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 1, 2 }));
            right.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 2, 1, 1 }));

            // Creates this case:
            /*  -------------------------
             *     Left     |     Right
             *   I  G1 G2   |   I  G1 G2
             *  -------------------------
             *   0  0  3    |   0  1  1
             *   1  1  1    |   1  1  2
             *   2  1  2    |   2  1  1
             *   3  2  1    |   3  2  1
             *   4  2  2
             *   5  3  1
             */

            /*
             * Merge will result in a DataFrame like:
             *   IL G1 G2     IR              Merged:
             *  -------------------------
             *   0  0  3                      0 - N
             *   1  1  1       0  1  1        1 - 0
             *   1  1  1       2  1  1        1 - 2
             *   2  1  2       1  1  2        2 - 1
             *   3  2  1       3  2  1        3 - 3
             *   4  2  2                      4 - N
             *   5  3  1                      5 - N
             */

            //Act
            var merge = left.Merge(right, new[] { "G1", "G2" }, new[] { "G1", "G2" });

            //Assert
            var expectedMerged = new (int? Left, int? Right)[] {
                (0, null),
                (1, 0),
                (1, 2),
                (2, 1),
                (3, 3),
                (4, null),
                (5, null)
            };

            Assert.Equal(expectedMerged.Length, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            for (long i = 0; i < expectedMerged.Length; i++)
            {
                MatchRowsOnMergedDataFrame(merge, left, right, i, expectedMerged[i].Left, expectedMerged[i].Right);
            }

        }

        [Fact]
        public void TestMerge_ByTwoColumns_Simple_ManyToMany_LeftJoin()
        {
            //Test left merge by to int type columns

            //Arrange
            var left = new DataFrame();
            left.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 3 }));
            left.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 3 }));

            var right = new DataFrame();
            right.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            right.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 0 }));
            right.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 0 }));

            // Creates this case:
            /*  ---------------------------
             *     Left    |    Right
             *   I  G1 G2  |   I  G1 G2
             *  ---------------------------
             *   0  1  1   |   0  1  1
             *   1  1  1   |   1  1  1
             *   2  3  3   |   2  0  0
             */

            /*
             * Merge will result in a DataFrame like:
             *   IL G1 G2     IR           Merged:
             *  -------------------------
             *   0  1  1      0  1  1       0 - 0
             *   0  1  1      1  1  1       0 - 1
             *   1  1  1      0  1  1       1 - 0
             *   1  1  1      1  1  1       1 - 1
             *   2  3  3                    2 - N
             */

            //Act
            var merge = left.Merge(right, new[] { "G1", "G2" }, new[] { "G1", "G2" });

            //Assert
            var expectedMerged = new (int? Left, int? Right)[] {
                (0, 0),
                (0, 1),
                (1, 0),
                (1, 1),
                (2, null)
            };

            Assert.Equal(expectedMerged.Length, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            for (long i = 0; i < expectedMerged.Length; i++)
            {
                MatchRowsOnMergedDataFrame(merge, left, right, i, expectedMerged[i].Left, expectedMerged[i].Right);
            }
        }

        [Fact]
        public void TestMerge_ByTwoColumns_Simple_ManyToMany_RightJoin()
        {
            //Test left merge by to int type columns

            //Arrange
            var left = new DataFrame();
            left.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 3 }));
            left.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 3 }));

            var right = new DataFrame();
            right.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            right.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 0 }));
            right.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 0 }));

            // Creates this case:
            /*  ---------------------------
             *     Left    |    Right
             *   I  G1 G2  |   I  G1 G2
             *  ---------------------------
             *   0  1  1   |   0  1  1
             *   1  1  1   |   1  1  1
             *   2  3  3   |   2  0  0
             */

            /*
             * Merge will result in a DataFrame like:
             *   IL G1 G2     IR           Merged:
             *  -------------------------
             *   0  1  1      0  1  1       0 - 0
             *   1  1  1      0  1  1       1 - 0
             *   0  1  1      1  1  1       0 - 1
             *   1  1  1      1  1  1       1 - 1
             *                2  0  0       N - 2
             */

            //Act
            var merge = left.Merge(right, new[] { "G1", "G2" }, new[] { "G1", "G2" }, joinAlgorithm: JoinAlgorithm.Right);

            //Assert
            var expectedMerged = new (int? Left, int? Right)[] {
                (0, 0),
                (1, 0),
                (0, 1),
                (1, 1),
                (null, 2)
            };

            Assert.Equal(expectedMerged.Length, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            for (long i = 0; i < expectedMerged.Length; i++)
            {
                MatchRowsOnMergedDataFrame(merge, left, right, i, expectedMerged[i].Left, expectedMerged[i].Right);
            }
        }

        [Fact]
        public void TestMerge_ByTwoColumns_Simple_ManyToMany_InnerJoin()
        {
            //Test left merge by to int type columns

            //Arrange
            var left = new DataFrame();
            left.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 3 }));
            left.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 3 }));

            var right = new DataFrame();
            right.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            right.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 0 }));
            right.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 0 }));

            // Creates this case:
            /*  ---------------------------
             *     Left    |    Right
             *   I  G1 G2  |   I  G1 G2
             *  ---------------------------
             *   0  1  1   |   0  1  1
             *   1  1  1   |   1  1  1
             *   2  3  3   |   2  0  0
             */

            /*
             * Merge will result in a DataFrame like:
             *   IL G1 G2     IR           Merged:
             *  -------------------------
             *   0  1  1      0  1  1       0 - 0
             *   1  1  1      0  1  1       1 - 0
             *   0  1  1      1  1  1       0 - 1
             *   1  1  1      1  1  1       1 - 1
             */

            //Act
            var merge = left.Merge(right, new[] { "G1", "G2" }, new[] { "G1", "G2" }, joinAlgorithm: JoinAlgorithm.Inner);

            //Assert
            var expectedMerged = new (int? Left, int? Right)[] {
                (0, 0),
                (1, 0),
                (0, 1),
                (1, 1)
            };

            Assert.Equal(expectedMerged.Length, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            for (long i = 0; i < expectedMerged.Length; i++)
            {
                MatchRowsOnMergedDataFrame(merge, left, right, i, expectedMerged[i].Left, expectedMerged[i].Right);
            }
        }

        [Fact]
        public void TestMerge_ByTwoColumns_Simple_ManyToMany_OuterJoin()
        {
            //Test left merge by to int type columns

            //Arrange
            var left = new DataFrame();
            left.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 3 }));
            left.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 3 }));

            var right = new DataFrame();
            right.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            right.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 0 }));
            right.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 0 }));

            // Creates this case:
            /*  ---------------------------
             *     Left    |    Right
             *   I  G1 G2  |   I  G1 G2
             *  ---------------------------
             *   0  1  1   |   0  1  1
             *   1  1  1   |   1  1  1
             *   2  3  3   |   2  0  0
             */

            /*
             * Merge will result in a DataFrame like:
             *   IL G1 G2     IR           Merged:
             *  -------------------------
             *   0  1  1      0  1  1       0 - 0
             *   0  1  1      1  1  1       0 - 1
             *   1  1  1      0  1  1       1 - 0
             *   1  1  1      1  1  1       1 - 1
             *   2  3  3                    2 - N
             *                2  0  0       N - 2
             */

            //Act
            var merge = left.Merge(right, new[] { "G1", "G2" }, new[] { "G1", "G2" }, joinAlgorithm: JoinAlgorithm.FullOuter);

            //Assert
            var expectedMerged = new (int? Left, int? Right)[] {
                (0, 0),
                (0, 1),
                (1, 0),
                (1, 1),
                (2, null),
                (null, 2)
            };

            Assert.Equal(expectedMerged.Length, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            for (long i = 0; i < expectedMerged.Length; i++)
            {
                MatchRowsOnMergedDataFrame(merge, left, right, i, expectedMerged[i].Left, expectedMerged[i].Right);
            }
        }

        [Fact]
        public void TestMerge_ByThreeColumns_OneToOne_LeftJoin()
        {
            //Test merge by LEFT join of int and string columns

            //Arrange
            var left = new DataFrame();
            left.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 2, 1 }));
            left.Columns.Add(new StringDataFrameColumn("G3", new[] { "A", "B", "C" }));

            var right = new DataFrame();
            right.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            right.Columns.Add(new Int32DataFrameColumn("G1", new[] { 0, 1, 1 }));
            right.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 2 }));
            right.Columns.Add(new StringDataFrameColumn("G3", new[] { "Z", "Y", "B" }));

            // Creates this case:
            /*  -----------------------------
             *      Left      |      Right
             *   I  G1 G2 G3  |   I  G1 G2 G3
             *  ------------------------------
             *   0  1  1  A   |   0  0  1  Z
             *   1  1  2  B   |   1  1  1  Y
             *   2  2  1  C   |   2  1  2  B
             */

            /*
             * Merge will result in a DataFrame like:
             *   IL G1 G2 G3    IR              Merged:
             *  -------------------------
             *   0  1  1  A                      0 - N
             *   1  1  2  B     2  1  2  B       1 - 2
             *   2  2  1  C                      2 - N
             */

            //Act
            var merge = left.Merge(right, new[] { "G1", "G2", "G3" }, new[] { "G1", "G2", "G3" });

            //Assert
            var expectedMerged = new (int? Left, int? Right)[] {
                (0, null),
                (1, 2),
                (2, null)
            };

            Assert.Equal(expectedMerged.Length, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            for (long i = 0; i < expectedMerged.Length; i++)
            {
                MatchRowsOnMergedDataFrame(merge, left, right, i, expectedMerged[i].Left, expectedMerged[i].Right);
            }
        }

        [Fact]
        public void TestMerge_ByThreeColumns_OneToOne_RightJoin()
        {
            //Test merge by RIGHT join of int and string columns

            //Arrange
            var left = new DataFrame();
            left.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G1", new[] { 1, 1, 2 }));
            left.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 2, 1 }));
            left.Columns.Add(new StringDataFrameColumn("G3", new[] { "A", "B", "C" }));

            var right = new DataFrame();
            right.Columns.Add(new Int32DataFrameColumn("Index", new[] { 0, 1, 2 }));
            right.Columns.Add(new Int32DataFrameColumn("G1", new[] { 0, 1, 1 }));
            right.Columns.Add(new Int32DataFrameColumn("G2", new[] { 1, 1, 2 }));
            right.Columns.Add(new StringDataFrameColumn("G3", new[] { "Z", "Y", "B" }));

            // Creates this case:
            /*  -----------------------------
             *      Left      |      Right
             *   I  G1 G2 G3  |   I  G1 G2 G3
             *  ------------------------------
             *   0  1  1  A   |   0  0  1  Z
             *   1  1  2  B   |   1  1  1  Y
             *   2  2  1  C   |   2  1  2  B
             */

            /*
             * Merge will result in a DataFrame like:
             *   IL G1 G2 G3    IR              Merged:
             *  -------------------------
             *                  0  0  1  Z       N - 0
             *                  1  1  1  Y       N - 1
             *   1  1  2  B     2  1  2  B       1 - 2
             */

            //Act
            var merge = left.Merge(right, new[] { "G1", "G2", "G3" }, new[] { "G1", "G2", "G3" }, joinAlgorithm: JoinAlgorithm.Right);

            //Assert
            var expectedMerged = new (int? Left, int? Right)[] {
                (null, 0),
                (null, 1),
                (1, 2)
            };

            Assert.Equal(expectedMerged.Length, merge.Rows.Count);
            Assert.Equal(merge.Columns.Count, left.Columns.Count + right.Columns.Count);

            for (long i = 0; i < expectedMerged.Length; i++)
            {
                MatchRowsOnMergedDataFrame(merge, left, right, i, expectedMerged[i].Left, expectedMerged[i].Right);
            }
        }

        [Fact]
        public void TestMerge_Issue5778()
        {
            DataFrame left = MakeDataFrameWithAllMutableColumnTypes(2, false);
            DataFrame right = MakeDataFrameWithAllMutableColumnTypes(1);

            DataFrame merge = left.Merge<int>(right, "Int", "Int");

            Assert.Equal(2, merge.Rows.Count);
            Assert.Equal(0, (int)merge.Columns["Int_left"][0]);
            Assert.Equal(1, (int)merge.Columns["Int_left"][1]);
            MatchRowsOnMergedDataFrame(merge, left, right, 0, 0, 0);
            MatchRowsOnMergedDataFrame(merge, left, right, 1, 1, 0);
        }

        [Fact]
        //Issue 6127
        public void TestMerge_CorrectColumnTypes()
        {
            DataFrame left = MakeDataFrameWithAllMutableColumnTypes(2, false);
            DataFrame right = MakeDataFrameWithAllMutableColumnTypes(1);

            DataFrame merge = left.Merge<int>(right, "Int", "Int");

            Assert.NotNull(merge.Columns.GetBooleanColumn("Bool_left"));
            Assert.NotNull(merge.Columns.GetBooleanColumn("Bool_right"));

            Assert.NotNull(merge.Columns.GetDecimalColumn("Decimal_left"));
            Assert.NotNull(merge.Columns.GetDecimalColumn("Decimal_right"));

            Assert.NotNull(merge.Columns.GetSingleColumn("Float_left"));
            Assert.NotNull(merge.Columns.GetSingleColumn("Float_right"));

            Assert.NotNull(merge.Columns.GetDoubleColumn("Double_left"));
            Assert.NotNull(merge.Columns.GetDoubleColumn("Double_right"));

            Assert.NotNull(merge.Columns.GetByteColumn("Byte_left"));
            Assert.NotNull(merge.Columns.GetByteColumn("Byte_right"));

            Assert.NotNull(merge.Columns.GetCharColumn("Char_left"));
            Assert.NotNull(merge.Columns.GetCharColumn("Char_right"));

            Assert.NotNull(merge.Columns.GetInt16Column("Short_left"));
            Assert.NotNull(merge.Columns.GetInt16Column("Short_right"));

            Assert.NotNull(merge.Columns.GetUInt16Column("Ushort_left"));
            Assert.NotNull(merge.Columns.GetUInt16Column("Ushort_right"));

            Assert.NotNull(merge.Columns.GetInt32Column("Int_left"));
            Assert.NotNull(merge.Columns.GetInt32Column("Int_right"));

            Assert.NotNull(merge.Columns.GetUInt32Column("Uint_left"));
            Assert.NotNull(merge.Columns.GetUInt32Column("Uint_right"));

            Assert.NotNull(merge.Columns.GetInt64Column("Long_left"));
            Assert.NotNull(merge.Columns.GetInt64Column("Long_right"));

            Assert.NotNull(merge.Columns.GetUInt64Column("Ulong_left"));
            Assert.NotNull(merge.Columns.GetUInt64Column("Ulong_right"));

            Assert.NotNull(merge.Columns.GetDateTimeColumn("DateTime_left"));
            Assert.NotNull(merge.Columns.GetDateTimeColumn("DateTime_right"));

        }

        [Fact]
        public void TestDescription()
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(10);

            DataFrame description = df.Description();
            DataFrameColumn descriptionColumn = description.Columns[0];
            Assert.Equal("Description", descriptionColumn.Name);
            Assert.Equal("Length (excluding null values)", descriptionColumn[0]);
            Assert.Equal("Max", descriptionColumn[1]);
            Assert.Equal("Min", descriptionColumn[2]);
            Assert.Equal("Mean", descriptionColumn[3]);
            for (int i = 1; i < description.Columns.Count - 1; i++)
            {
                DataFrameColumn column = description.Columns[i];
                Assert.Equal(df.Columns[i - 1].Name, column.Name);
                Assert.Equal(4, column.Length);
                Assert.Equal((float)9, column[0]);
                Assert.Equal((float)9, column[1]);
                Assert.Equal((float)0, column[2]);
                Assert.Equal((float)4, column[3]);
            }

            // Explicitly check the dateTimes column
            DataFrameColumn dateTimeColumn = description.Columns[description.Columns.Count - 1];
            Assert.Equal("DateTime", dateTimeColumn.Name);
            Assert.Equal(4, dateTimeColumn.Length);
            Assert.Equal((float)9, dateTimeColumn[0]);
            Assert.Null(dateTimeColumn[1]);
            Assert.Null(dateTimeColumn[2]);
            Assert.Null(dateTimeColumn[3]);
        }

        [Fact]
        public void TestInfo()
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(10);

            // Add a column manually here until we fix https://github.com/dotnet/corefxlab/issues/2784
            PrimitiveDataFrameColumn<DateTime> dateTimes = new PrimitiveDataFrameColumn<DateTime>("DateTimes");
            for (int i = 0; i < 10; i++)
            {
                dateTimes.Append(DateTime.Parse("2019/01/01"));
            }
            df.Columns.Add(dateTimes);

            DataFrame Info = df.Info();
            DataFrameColumn infoColumn = Info.Columns[0];
            Assert.Equal("Info", infoColumn.Name);
            Assert.Equal("Length (excluding null values)", infoColumn[1]);
            Assert.Equal("DataType", infoColumn[0]);

            for (int i = 1; i < Info.Columns.Count; i++)
            {
                DataFrameColumn column = Info.Columns[i];
                Assert.Equal(df.Columns[i - 1].DataType.ToString(), column[0].ToString());
                Assert.Equal(2, column.Length);
            }
        }

        [Fact]
        public void TestDropNulls()
        {
            DataFrame df = MakeDataFrameWithAllMutableColumnTypes(20);
            DataFrame anyNulls = df.DropNulls();
            Assert.Equal(19, anyNulls.Rows.Count);

            DataFrame allNulls = df.DropNulls(DropNullOptions.All);
            Assert.Equal(19, allNulls.Rows.Count);
        }

        [Fact]
        public void TestInsertMismatchedColumnToEmptyDataFrame()
        {
            DataFrame df = new DataFrame();
            DataFrameColumn dataFrameColumn1 = new Int32DataFrameColumn("Int1");
            df.Columns.Insert(0, dataFrameColumn1);

            // should throw exception as column sizes are mismatched.

            Assert.Throws<ArgumentException>(() => df.Columns.Insert(1, new Int32DataFrameColumn("Int2", Enumerable.Range(0, 5).Select(x => x))));
        }

        [Fact]
        public void TestFillNulls()
        {
            DataFrame df = MakeDataFrameWithTwoColumns(20);
            Assert.Null(df[10, 0]);
            DataFrame fillNulls = df.FillNulls(1000);
            Assert.Equal(1000, (int)fillNulls[10, 1]);
            Assert.Null(df[10, 0]);
            df.FillNulls(1000, true);
            Assert.Equal(1000, df[10, 1]);

            StringDataFrameColumn strColumn = new StringDataFrameColumn("String", 0);
            strColumn.Append(null);
            strColumn.Append(null);
            Assert.Equal(2, strColumn.Length);
            Assert.Equal(2, strColumn.NullCount);
            DataFrameColumn filled = strColumn.FillNulls("foo");
            Assert.Equal(2, strColumn.Length);
            Assert.Equal(2, strColumn.NullCount);
            Assert.Equal(2, filled.Length);
            Assert.Equal(0, filled.NullCount);
            Assert.Equal("foo", filled[0]);
            Assert.Equal("foo", filled[1]);
            Assert.Null(strColumn[0]);
            Assert.Null(strColumn[1]);

            // In place
            strColumn.FillNulls("foo", true);
            Assert.Equal(2, strColumn.Length);
            Assert.Equal(0, strColumn.NullCount);
            Assert.Equal("foo", strColumn[0]);
            Assert.Equal("foo", strColumn[1]);

            // ArrowStringColumn (not inplace)
            ArrowStringDataFrameColumn arrowColumn = CreateArrowStringColumn(3);
            Assert.Equal(3, arrowColumn.Length);
            Assert.Equal(1, arrowColumn.NullCount);
            Assert.Null(arrowColumn[1]);
            ArrowStringDataFrameColumn arrowColumnFilled = arrowColumn.FillNulls("foo");
            Assert.Equal(3, arrowColumn.Length);
            Assert.Equal(1, arrowColumn.NullCount);
            Assert.Equal(3, arrowColumnFilled.Length);
            Assert.Equal(0, arrowColumnFilled.NullCount);
            Assert.Equal("foo", arrowColumnFilled[1]);
            Assert.Equal(arrowColumn[0], arrowColumnFilled[0]);
            Assert.Equal(arrowColumn[2], arrowColumnFilled[2]);
        }

        [Fact]
        public void TestValueCounts()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10, withNulls: false);
            DataFrame valueCounts = df.Columns["Bool"].ValueCounts();
            Assert.Equal(2, valueCounts.Rows.Count);
            Assert.Equal((long)5, valueCounts.Columns["Counts"][0]);
            Assert.Equal((long)5, valueCounts.Columns["Counts"][1]);
        }

        [Fact]
        public void TestApplyElementwiseNullCount()
        {
            DataFrame df = MakeDataFrameWithTwoColumns(10);
            Int32DataFrameColumn column = df.Columns["Int1"] as Int32DataFrameColumn;
            Assert.Equal(1, column.NullCount);

            // Change all existing values to null
            column.ApplyElementwise((int? value, long rowIndex) =>
            {
                if (!(value is null))
                    return null;
                return value;
            });
            Assert.Equal(column.Length, column.NullCount);

            // Don't change null values
            column.ApplyElementwise((int? value, long rowIndex) =>
            {
                return value;
            });
            Assert.Equal(column.Length, column.NullCount);

            // Change all null values to real values
            column.ApplyElementwise((int? value, long rowIndex) =>
            {
                return 5;
            });
            Assert.Equal(0, column.NullCount);

            // Don't change real values
            column.ApplyElementwise((int? value, long rowIndex) =>
            {
                return value;
            });
            Assert.Equal(0, column.NullCount);

        }

        [Theory]
        [InlineData(10, 5)]
        [InlineData(20, 20)]
        public void TestClone(int dfLength, int intDfLength)
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(dfLength, withNulls: true);
            DataFrame intDf = MakeDataFrameWithTwoColumns(intDfLength, false);
            Int32DataFrameColumn intColumn = intDf.Columns["Int1"] as Int32DataFrameColumn;
            DataFrame clone = df[intColumn];
            Assert.Equal(intDfLength, clone.Rows.Count);
            Assert.Equal(df.Columns.Count, clone.Columns.Count);
            for (int i = 0; i < df.Columns.Count; i++)
            {
                DataFrameColumn dfColumn = df.Columns[i];
                DataFrameColumn cloneColumn = clone.Columns[i];
                for (long r = 0; r < clone.Rows.Count; r++)
                {
                    Assert.Equal(dfColumn[r], cloneColumn[r]);
                }
            }
        }

        [Fact]
        public void TestColumnCreationFromExisitingColumn()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10);
            BooleanDataFrameColumn bigInts = new BooleanDataFrameColumn("BigInts", df.Columns["Int"].ElementwiseGreaterThan(5));
            for (int i = 0; i < 10; i++)
            {
                if (i <= 5)
                    Assert.False(bigInts[i]);
                else
                    Assert.True(bigInts[i]);
            }
        }

        [Fact]
        public void TestColumns()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10);
            IReadOnlyList<DataFrameColumn> columns = df.Columns;
            int i = 0;
            Assert.Equal(columns.Count, df.Columns.Count);
            foreach (DataFrameColumn dataFrameColumn in columns)
            {
                Assert.Equal(dataFrameColumn, df.Columns[i++]);
            }

        }

        [Fact]
        public void TestRows()
        {
            DataFrame df = MakeDataFrameWithAllColumnTypes(10);
            DataFrameRowCollection rows = df.Rows;
            Assert.Equal(10, rows.Count);
            DataFrameRow firstRow = rows[0];
            object firstValue = firstRow[0];
            Assert.Equal(df[0, 0], firstValue);
            long rowCount = 0;
            foreach (DataFrameRow row in rows)
            {
                int columnIndex = 0;
                foreach (var value in row)
                {
                    Assert.Equal(df.Columns[columnIndex][rowCount], value);
                    columnIndex++;
                }
                rowCount++;
            }
            Assert.Equal(df.Rows.Count, rowCount);

            DataFrameRow nullRow = rows[5];
            int intColumnIndex = df.Columns.IndexOf("Int");
            Assert.Equal(1, df.Columns[intColumnIndex].NullCount);
            nullRow[intColumnIndex] = 5;
            Assert.Equal(0, df.Columns[intColumnIndex].NullCount);
            nullRow[intColumnIndex] = null;
            Assert.Equal(1, df.Columns[intColumnIndex].NullCount);
        }

        [Fact]
        public void TestMutationOnRows()
        {
            DataFrame df = MakeDataFrameWithNumericColumns(10);
            DataFrameRowCollection rows = df.Rows;

            foreach (DataFrameRow row in rows)
            {
                for (int i = 0; i < df.Columns.Count; i++)
                {
                    DataFrameColumn column = df.Columns[i];
                    row[i] = Convert.ChangeType(12, column.DataType);
                }
            }

            foreach (var column in df.Columns)
            {
                foreach (var value in column)
                {
                    Assert.Equal("12", value.ToString());
                }
            }
        }

        [Fact]
        public void TestAppendRows()
        {
            DataFrame df = MakeDataFrame<float, bool>(10);
            DataFrame df2 = MakeDataFrame<int, bool>(5);
            Assert.Equal(10, df.Rows.Count);
            Assert.Equal(1, df.Columns[0].NullCount);
            Assert.Equal(1, df.Columns[1].NullCount);

            DataFrame ret = df.Append(df2.Rows, inPlace: false);
            Assert.Equal(10, df.Rows.Count);
            Assert.Equal(1, df.Columns[0].NullCount);
            Assert.Equal(1, df.Columns[1].NullCount);

            Verify(ret, df, df2);

            void Verify(DataFrame ret, DataFrame check1, DataFrame check2)
            {
                Assert.Equal(15, ret.Rows.Count);
                Assert.Equal(2, ret.Columns[0].NullCount);
                Assert.Equal(2, ret.Columns[1].NullCount);
                for (long i = 0; i < ret.Rows.Count; i++)
                {
                    DataFrameRow row = ret.Rows[i];
                    for (int j = 0; j < check1.Columns.Count; j++)
                    {
                        if (i < check1.Rows.Count)
                        {
                            Assert.Equal(row[j], check1.Rows[i][j]);
                        }
                        else
                        {
                            Assert.Equal(row[j]?.ToString(), (check2.Rows[i - check1.Rows.Count][j])?.ToString());
                        }
                    }
                }
            }

            DataFrame dfClone = df.Clone();
            df.Append(df2.Rows, inPlace: true);
            Verify(df, dfClone, df2);
        }

        [Fact]
        public void TestAppendRow()
        {
            DataFrame df = MakeDataFrame<int, bool>(10);
            df.Append(new List<object> { 5, true }, inPlace: true);
            Assert.Equal(11, df.Rows.Count);
            Assert.Equal(1, df.Columns[0].NullCount);
            Assert.Equal(1, df.Columns[1].NullCount);

            DataFrame ret = df.Append(new List<object> { 5, true });
            Assert.Equal(12, ret.Rows.Count);
            Assert.Equal(1, ret.Columns[0].NullCount);
            Assert.Equal(1, ret.Columns[1].NullCount);

            df.Append(new List<object> { 100 }, inPlace: true);
            Assert.Equal(12, df.Rows.Count);
            Assert.Equal(1, df.Columns[0].NullCount);
            Assert.Equal(2, df.Columns[1].NullCount);

            ret = df.Append(new List<object> { 100 }, inPlace: false);
            Assert.Equal(13, ret.Rows.Count);
            Assert.Equal(1, ret.Columns[0].NullCount);
            Assert.Equal(3, ret.Columns[1].NullCount);

            df.Append(new List<object> { null, null }, inPlace: true);
            Assert.Equal(13, df.Rows.Count);
            Assert.Equal(2, df.Columns[0].NullCount);
            Assert.Equal(3, df.Columns[1].NullCount);
            ret = df.Append(new List<object> { null, null }, inPlace: false);
            Assert.Equal(14, ret.Rows.Count);
            Assert.Equal(3, ret.Columns[0].NullCount);
            Assert.Equal(4, ret.Columns[1].NullCount);

            df.Append(new Dictionary<string, object> { { "Column1", (object)5 }, { "Column2", false } }, inPlace: true);
            Assert.Equal(14, df.Rows.Count);
            Assert.Equal(2, df.Columns[0].NullCount);
            Assert.Equal(3, df.Columns[1].NullCount);
            ret = df.Append(new Dictionary<string, object> { { "Column1", (object)5 }, { "Column2", false } }, inPlace: false);
            Assert.Equal(15, ret.Rows.Count);
            Assert.Equal(2, ret.Columns[0].NullCount);
            Assert.Equal(3, ret.Columns[1].NullCount);

            df.Append(new Dictionary<string, object> { { "Column1", 5 } }, inPlace: true);
            Assert.Equal(15, df.Rows.Count);

            Assert.Equal(15, df.Columns["Column1"].Length);
            Assert.Equal(15, df.Columns["Column2"].Length);
            Assert.Equal(2, df.Columns[0].NullCount);
            Assert.Equal(4, df.Columns[1].NullCount);
            ret = df.Append(new Dictionary<string, object> { { "Column1", 5 } }, inPlace: false);
            Assert.Equal(16, ret.Rows.Count);

            Assert.Equal(16, ret.Columns["Column1"].Length);
            Assert.Equal(16, ret.Columns["Column2"].Length);
            Assert.Equal(2, ret.Columns[0].NullCount);
            Assert.Equal(5, ret.Columns[1].NullCount);

            df.Append(new Dictionary<string, object> { { "Column2", false } }, inPlace: true);
            Assert.Equal(16, df.Rows.Count);
            Assert.Equal(16, df.Columns["Column1"].Length);
            Assert.Equal(16, df.Columns["Column2"].Length);
            Assert.Equal(3, df.Columns[0].NullCount);
            Assert.Equal(4, df.Columns[1].NullCount);
            ret = df.Append(new Dictionary<string, object> { { "Column2", false } }, inPlace: false);
            Assert.Equal(17, ret.Rows.Count);
            Assert.Equal(17, ret.Columns["Column1"].Length);
            Assert.Equal(17, ret.Columns["Column2"].Length);
            Assert.Equal(4, ret.Columns[0].NullCount);
            Assert.Equal(4, ret.Columns[1].NullCount);

            df.Append((IEnumerable<object>)null, inPlace: true);
            Assert.Equal(17, df.Rows.Count);
            Assert.Equal(17, df.Columns["Column1"].Length);
            Assert.Equal(17, df.Columns["Column2"].Length);
            Assert.Equal(4, df.Columns[0].NullCount);
            Assert.Equal(5, df.Columns[1].NullCount);
            ret = df.Append((IEnumerable<object>)null, inPlace: false);
            Assert.Equal(18, ret.Rows.Count);
            Assert.Equal(18, ret.Columns["Column1"].Length);
            Assert.Equal(18, ret.Columns["Column2"].Length);
            Assert.Equal(5, ret.Columns[0].NullCount);
            Assert.Equal(6, ret.Columns[1].NullCount);

            // DataFrame must remain usable even if Append throws
            Assert.Throws<FormatException>(() => df.Append(new List<object> { 5, "str" }, inPlace: true));
            Assert.Throws<FormatException>(() => df.Append(new Dictionary<string, object> { { "Column2", "str" } }, inPlace: true));
            Assert.Throws<ArgumentException>(() => df.Append(new List<object> { 5, true, true }, inPlace: true));

            df.Append(inPlace: true);
            Assert.Equal(18, df.Rows.Count);
            Assert.Equal(18, df.Columns["Column1"].Length);
            Assert.Equal(18, df.Columns["Column2"].Length);
            Assert.Equal(5, df.Columns[0].NullCount);
            Assert.Equal(6, df.Columns[1].NullCount);

            ret = df.Append(inPlace: false);
            Assert.Equal(18, df.Rows.Count);
            Assert.Equal(18, df.Columns["Column1"].Length);
            Assert.Equal(18, df.Columns["Column2"].Length);
            Assert.Equal(5, df.Columns[0].NullCount);
            Assert.Equal(6, df.Columns[1].NullCount);
            Assert.Equal(19, ret.Rows.Count);
            Assert.Equal(19, ret.Columns["Column1"].Length);
            Assert.Equal(19, ret.Columns["Column2"].Length);
            Assert.Equal(6, ret.Columns[0].NullCount);
            Assert.Equal(7, ret.Columns[1].NullCount);
        }

        [Fact]
        public void TestAppendEmptyValue()
        {
            DataFrame df = MakeDataFrame<int, bool>(10);
            df.Append(new List<object> { "", true }, inPlace: true);
            Assert.Equal(11, df.Rows.Count);
            Assert.Equal(2, df.Columns[0].NullCount);
            Assert.Equal(1, df.Columns[1].NullCount);

            StringDataFrameColumn column = new StringDataFrameColumn("Strings", Enumerable.Range(0, 11).Select(x => x.ToString()));
            df.Columns.Add(column);

            df.Append(new List<object> { 1, true, "" }, inPlace: true);
            Assert.Equal(12, df.Rows.Count);
            Assert.Equal(2, df.Columns[0].NullCount);
            Assert.Equal(1, df.Columns[1].NullCount);
            Assert.Equal(0, df.Columns[2].NullCount);

            df.Append(new List<object> { 1, true, null }, inPlace: true);
            Assert.Equal(13, df.Rows.Count);
            Assert.Equal(1, df.Columns[2].NullCount);
        }

        [Fact]
        public void TestApply()
        {
            int[] values = { 1, 2, 3, 4, 5 };
            var col = new Int32DataFrameColumn("Ints", values);
            PrimitiveDataFrameColumn<double> newCol = col.Apply(i => i + 0.5d);

            Assert.Equal(values.Length, newCol.Length);

            for (int i = 0; i < newCol.Length; i++)
            {
                Assert.Equal(col[i], values[i]); // Make sure values didn't change
                Assert.Equal(newCol[i], values[i] + 0.5d);
            }
        }

        [Fact]
        public void TestDataFrameCreate()
        {
            int length = 10;
            void AssertLengthTypeAndValues(DataFrameColumn column, Type type)
            {
                Assert.Equal(column.DataType, type);
                Assert.Equal(length, column.Length);
                for (long i = 0; i < column.Length; i++)
                {
                    Assert.Equal(i.ToString(), column[i].ToString());
                }
            }
            DataFrameColumn stringColumn = DataFrameColumn.Create("String", Enumerable.Range(0, length).Select(x => x.ToString()));
            AssertLengthTypeAndValues(stringColumn, typeof(string));
            DataFrameColumn byteColumn = DataFrameColumn.Create("Byte", Enumerable.Range(0, length).Select(x => (byte)x));
            AssertLengthTypeAndValues(byteColumn, typeof(byte));
            DataFrameColumn decimalColumn = DataFrameColumn.Create("Decimal", Enumerable.Range(0, length).Select(x => (decimal)x));
            AssertLengthTypeAndValues(decimalColumn, typeof(decimal));
            DataFrameColumn doubleColumn = DataFrameColumn.Create("Double", Enumerable.Range(0, length).Select(x => (double)x));
            AssertLengthTypeAndValues(doubleColumn, typeof(double));
            DataFrameColumn floatColumn = DataFrameColumn.Create("Float", Enumerable.Range(0, length).Select(x => (float)x));
            AssertLengthTypeAndValues(floatColumn, typeof(float));
            DataFrameColumn intColumn = DataFrameColumn.Create("Int", Enumerable.Range(0, length).Select(x => x));
            AssertLengthTypeAndValues(intColumn, typeof(int));
            DataFrameColumn longColumn = DataFrameColumn.Create("Long", Enumerable.Range(0, length).Select(x => (long)x));
            AssertLengthTypeAndValues(longColumn, typeof(long));
            DataFrameColumn sbyteColumn = DataFrameColumn.Create("Sbyte", Enumerable.Range(0, length).Select(x => (sbyte)x));
            AssertLengthTypeAndValues(sbyteColumn, typeof(sbyte));
            DataFrameColumn shortColumn = DataFrameColumn.Create("Short", Enumerable.Range(0, length).Select(x => (short)x));
            AssertLengthTypeAndValues(shortColumn, typeof(short));
            DataFrameColumn uintColumn = DataFrameColumn.Create("Uint", Enumerable.Range(0, length).Select(x => (uint)x));
            AssertLengthTypeAndValues(uintColumn, typeof(uint));
            DataFrameColumn ulongColumn = DataFrameColumn.Create("Ulong", Enumerable.Range(0, length).Select(x => (ulong)x));
            AssertLengthTypeAndValues(ulongColumn, typeof(ulong));
            DataFrameColumn ushortColumn = DataFrameColumn.Create("Ushort", Enumerable.Range(0, length).Select(x => (ushort)x));
            AssertLengthTypeAndValues(ushortColumn, typeof(ushort));
        }

        [Fact]
        public void TestBinaryOperationsOnExplodedNumericColumns()
        {
            DataFrame df = MakeDataFrameWithNumericAndBoolColumns(10, withNulls: false);
            Int32DataFrameColumn ints = df.Columns["Int"] as Int32DataFrameColumn;
            Int32DataFrameColumn res = ints.Add(1).Subtract(1).Multiply(10).Divide(10).LeftShift(2).RightShift(2);
            Assert.True(res.ElementwiseEquals(ints).All());
            Assert.True(res.ElementwiseGreaterThanOrEqual(ints).All());
            Assert.True(res.ElementwiseLessThanOrEqual(ints).All());
            Assert.False(res.ElementwiseNotEquals(ints).All());
            Assert.False(res.ElementwiseGreaterThan(ints).All());
            Assert.False(res.ElementwiseLessThan(ints).All());

            // Test inPlace
            Int32DataFrameColumn inPlace = ints.Add(1, inPlace: true).Subtract(1, inPlace: true).Multiply(10, inPlace: true).Divide(10, inPlace: true).LeftShift(2, inPlace: true).RightShift(2, inPlace: true).Add(100, inPlace: true);
            Assert.True(inPlace.ElementwiseEquals(ints).All());
            Assert.True(inPlace.ElementwiseGreaterThanOrEqual(ints).All());
            Assert.True(inPlace.ElementwiseLessThanOrEqual(ints).All());
            Assert.False(inPlace.ElementwiseNotEquals(ints).All());
            Assert.False(inPlace.ElementwiseGreaterThan(ints).All());
            Assert.False(inPlace.ElementwiseLessThan(ints).All());

            Assert.False(inPlace.ElementwiseEquals(res).All());
            Assert.True(inPlace.ElementwiseGreaterThanOrEqual(res).All());
            Assert.False(inPlace.ElementwiseLessThanOrEqual(res).All());
            Assert.True(inPlace.ElementwiseNotEquals(res).All());
            Assert.True(inPlace.ElementwiseGreaterThan(res).All());
            Assert.False(inPlace.ElementwiseLessThan(res).All());

            // Test Bool column
            BooleanDataFrameColumn bools = df.Columns["Bool"] as BooleanDataFrameColumn;
            BooleanDataFrameColumn allFalse = bools.Or(true).And(true).Xor(true);
            Assert.True(allFalse.ElementwiseEquals(false).All());

            // Test inPlace
            BooleanDataFrameColumn inPlaceAllFalse = bools.Or(true, inPlace: true).And(true, inPlace: true).Xor(true, inPlace: true);
            Assert.True(inPlaceAllFalse.ElementwiseEquals(bools).All());

            // Test Reverse Operations
            Int32DataFrameColumn reverse = ints.ReverseAdd(1).ReverseSubtract(1).ReverseMultiply(-1);
            Assert.True(reverse.ElementwiseEquals(ints).All());

            // Test inPlace
            Int32DataFrameColumn reverseInPlace = ints.ReverseAdd(1, inPlace: true).ReverseSubtract(1, inPlace: true).ReverseMultiply(-1, inPlace: true).ReverseDivide(100, inPlace: true);
            Assert.True(reverseInPlace.ElementwiseEquals(ints).All());
            Assert.False(reverseInPlace.ElementwiseEquals(reverse).All());
        }

        [Fact]
        public void TestArrowStringApply()
        {
            ArrowStringDataFrameColumn column = CreateArrowStringColumn(10);
            ArrowStringDataFrameColumn ret = column.Apply((string cur) =>
            {
                if (cur != null)
                {
                    return cur + "123";
                }
                return null;
            });
            for (long i = 0; i < column.Length; i++)
            {
                if (column[i] != null)
                {
                    Assert.Equal(column[i] + "123", ret[i]);
                }
                else
                {
                    Assert.Null(ret[i]);
                }
            }
            Assert.Equal(1, ret.NullCount);

            // Test null counts
            ret = column.Apply((string cur) =>
            {
                return null;
            });
            Assert.Equal(column.Length, ret.NullCount);
        }

        [Fact]
        public void GetColumnTests()
        {
            DataFrame dataFrame = MakeDataFrameWithAllColumnTypes(10);
            PrimitiveDataFrameColumn<int> primitiveInts = dataFrame.Columns.GetPrimitiveColumn<int>("Int");
            Assert.NotNull(primitiveInts);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetPrimitiveColumn<float>("Int"));

            StringDataFrameColumn strings = dataFrame.Columns.GetStringColumn("String");
            Assert.NotNull(strings);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetStringColumn("ArrowString"));

            ArrowStringDataFrameColumn arrowStrings = dataFrame.Columns.GetArrowStringColumn("ArrowString");
            Assert.NotNull(arrowStrings);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetArrowStringColumn("String"));

            ByteDataFrameColumn bytes = dataFrame.Columns.GetByteColumn("Byte");
            Assert.NotNull(bytes);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Byte"));

            Int32DataFrameColumn ints = dataFrame.Columns.GetInt32Column("Int");
            Assert.NotNull(ints);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Int"));

            BooleanDataFrameColumn bools = dataFrame.Columns.GetBooleanColumn("Bool");
            Assert.NotNull(bools);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Bool"));

            CharDataFrameColumn chars = dataFrame.Columns.GetCharColumn("Char");
            Assert.NotNull(chars);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Char"));

            DecimalDataFrameColumn decimals = dataFrame.Columns.GetDecimalColumn("Decimal");
            Assert.NotNull(decimals);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Decimal"));

            DoubleDataFrameColumn doubles = dataFrame.Columns.GetDoubleColumn("Double");
            Assert.NotNull(doubles);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Double"));

            SingleDataFrameColumn singles = dataFrame.Columns.GetSingleColumn("Float");
            Assert.NotNull(singles);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetDoubleColumn("Float"));

            Int64DataFrameColumn longs = dataFrame.Columns.GetInt64Column("Long");
            Assert.NotNull(longs);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Long"));

            SByteDataFrameColumn sbytes = dataFrame.Columns.GetSByteColumn("Sbyte");
            Assert.NotNull(sbytes);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Sbyte"));

            Int16DataFrameColumn shorts = dataFrame.Columns.GetInt16Column("Short");
            Assert.NotNull(shorts);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Short"));

            UInt32DataFrameColumn uints = dataFrame.Columns.GetUInt32Column("Uint");
            Assert.NotNull(uints);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Uint"));

            UInt64DataFrameColumn ulongs = dataFrame.Columns.GetUInt64Column("Ulong");
            Assert.NotNull(ulongs);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Ulong"));

            UInt16DataFrameColumn ushorts = dataFrame.Columns.GetUInt16Column("Ushort");
            Assert.NotNull(ushorts);
            Assert.Throws<ArgumentException>(() => dataFrame.Columns.GetSingleColumn("Ushort"));

        }

        [Fact]
        public void TestMeanMedian()
        {
            DataFrame df = MakeDataFrameWithNumericColumns(10, true, 0);

            Assert.Equal(40.0 / 9.0, df["Decimal"].Mean());
            Assert.Equal(4, df["Decimal"].Median());

        }
    }
}
