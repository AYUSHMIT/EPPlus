﻿/*************************************************************************************************
  Required Notice: Copyright (C) EPPlus Software AB. 
  This software is licensed under PolyForm Noncommercial License 1.0.0 
  and may only be used for noncommercial purposes 
  https://polyformproject.org/licenses/noncommercial/1.0.0/

  A commercial license to use this software can be purchased at https://epplussoftware.com
 *************************************************************************************************
  Date               Author                       Change
 *************************************************************************************************
  05/07/2023         EPPlus Software AB           EPPlus v7
 *************************************************************************************************/

using OfficeOpenXml.FormulaParsing.Excel.Functions.Helpers;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Metadata;
using OfficeOpenXml.FormulaParsing.FormulaExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OfficeOpenXml.FormulaParsing.Excel.Functions.Statistical
{
    [FunctionMetadata(
    Category = ExcelFunctionCategory.Statistical,
    EPPlusVersion = "7.0",
    Description = "Returns the two-tailed Students t-distribution. The Students t-distribution is used for hypothesis testing with small samples.")]
    internal class TDist2t : ExcelFunction
    {
        public override string NamespacePrefix => "_xlfn.";
        public override int ArgumentMinLength => 2;

        public override CompileResult Execute(IList<FunctionArgument> arguments, ParsingContext context)
        {
            var x = ArgToDecimal(arguments, 0);
            var degreesOfFreedom = ArgToDecimal(arguments, 1);

            //Excel rounds degreesOfFreedom to the nearest int, so we do the same.

            degreesOfFreedom = System.Math.Floor(degreesOfFreedom);
            if (degreesOfFreedom < 1)
            {
                return CreateResult(eErrorType.Num);
            }

            if (x < 0)
            {
                return CreateResult(eErrorType.Num);
            }

            var result = 2 * (1 - StudenttHelper.CumulativeDistributionFunction(x, degreesOfFreedom));

            return CreateResult(result, DataType.Decimal);
        }
    }
}
