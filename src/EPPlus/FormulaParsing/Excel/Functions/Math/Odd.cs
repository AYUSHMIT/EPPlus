﻿/*************************************************************************************************
  Required Notice: Copyright (C) EPPlus Software AB. 
  This software is licensed under PolyForm Noncommercial License 1.0.0 
  and may only be used for noncommercial purposes 
  https://polyformproject.org/licenses/noncommercial/1.0.0/

  A commercial license to use this software can be purchased at https://epplussoftware.com
 *************************************************************************************************
  Date               Author                       Change
 *************************************************************************************************
  01/27/2020         EPPlus Software AB       Initial release EPPlus 5
 *************************************************************************************************/
using OfficeOpenXml.FormulaParsing.ExpressionGraph;
using OfficeOpenXml.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OfficeOpenXml.FormulaParsing.Excel.Functions.Math
{
    internal class Odd : ExcelFunction
    {
        public override CompileResult Execute(IEnumerable<FunctionArgument> arguments, ParsingContext context)
        {
            ValidateArguments(arguments, 1);
            var arg = arguments.ElementAt(0).Value;
            if (!IsNumeric(arg)) return new CompileResult(eErrorType.Value);
            var number = ConvertUtil.GetValueDouble(arg);
            if(number % 1 > 0)
            {
                if (number >= 0)
                {
                    number = number - (number % 1) + 1;
                }
                else
                {
                    number = number - (number % 1) - 1;
                }
            }
            var intNumber = Convert.ToInt32(number);
            if(intNumber % 2 == 0)
            {
                intNumber = intNumber >= 0 ? intNumber + 1 : intNumber - 1;
            }
            return CreateResult(intNumber, DataType.Integer);
        }
    }
}
