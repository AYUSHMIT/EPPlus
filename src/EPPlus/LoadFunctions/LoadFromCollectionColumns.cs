﻿/*************************************************************************************************
  Required Notice: Copyright (C) EPPlus Software AB. 
  This software is licensed under PolyForm Noncommercial License 1.0.0 
  and may only be used for noncommercial purposes 
  https://polyformproject.org/licenses/noncommercial/1.0.0/

  A commercial license to use this software can be purchased at https://epplussoftware.com
 *************************************************************************************************
  Date               Author                       Change
 *************************************************************************************************
  08/286/2021         EPPlus Software AB       EPPlus 5.7.5
 *************************************************************************************************/
using OfficeOpenXml.Attributes;
using OfficeOpenXml.Table;
using OfficeOpenXml.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OfficeOpenXml.LoadFunctions
{
    internal class LoadFromCollectionColumns<T>
    {
        public LoadFromCollectionColumns(BindingFlags bindingFlags)
            : this(bindingFlags, new List<string>())
        {

        }

        public LoadFromCollectionColumns(BindingFlags bindingFlags, List<string> sortOrderColumns)
        : this(bindingFlags, sortOrderColumns, null)
        {
            
        }

        public LoadFromCollectionColumns(BindingFlags bindingFlags, List<string> sortOrderColumns, MemberInfo[] members)
        {
            _bindingFlags = bindingFlags;
            _sortOrderColumns = sortOrderColumns;
            _filterMembers = members;
            _includedTypes = new HashSet<Type>
            {
                typeof(T)
            };
            _members = new Dictionary<Type, HashSet<string>>();
            if (members != null && members.Length > 0)
            {
                foreach (var member in members)
                {
                    AddMember(member);
                }
            }
        }


        private readonly BindingFlags _bindingFlags;
        private readonly List<string> _sortOrderColumns;
        private readonly Dictionary<Type, HashSet<string>> _members;
        private MemberInfo[] _filterMembers;
        private readonly HashSet<Type> _includedTypes;
        private const int SortOrderOffset = ExcelPackage.MaxColumns;

        internal List<ColumnInfo> Setup()
        {
            var result = new List<ColumnInfo>();
            var t = typeof(T);
            var ut = Nullable.GetUnderlyingType(t);
            if (ut != null)
            {
                t = ut;
            }

            bool sort=SetupInternal(t, result, null);
            if (sort)
            {
                ReindexAndSortColumns(result);
            }
            return result;
        }

        private void AddMember(MemberInfo member)
        {
            if (!_members.ContainsKey(member.DeclaringType))
            {
                _members.Add(member.DeclaringType, new HashSet<string>());
            }
            _members[member.DeclaringType].Add(member.Name);
        }

        internal void ValidateType(MemberInfo member)
        {
            var isValid = false;
            foreach (var includedType in _includedTypes)
            {

                if (member.DeclaringType == includedType
                    || member.DeclaringType.IsAssignableFrom(includedType)
                    || member.DeclaringType.IsSubclassOf(includedType))
                {
                    isValid = true;
                    break;
                }
            }
            if (!isValid) throw new InvalidCastException("Supplied properties in parameter Properties must be of the same type as T (or an assignable type from T)");
        }

        private List<ListType> CopyList<ListType>(List<ListType> source)
        {
            if (source == null) return null;
            var copy = new List<ListType>();
            source.ForEach(x => copy.Add(x));
            return copy;
        }

        private bool ShouldIgnoreMember(MemberInfo member, bool isNested)
        {
            if (member == null) return true;
            if (member.HasPropertyOfType<EpplusIgnore>()) return true;
            if (_members.Count == 0) return false;
            //ignore by member info only works for the first level (outer class)
            if (isNested) return false;
            return !(_members.ContainsKey(member.DeclaringType) && _members[member.DeclaringType].Contains(member.Name));
        }

        private bool SetupInternal(Type type, List<ColumnInfo> result, List<int> sortOrderListArg, bool isNestedClass = false, string path = null, string headerPrefix = null)
        {
            var sort = false;
            var members = _filterMembers ?? type.GetProperties(_bindingFlags);
            if (type.HasMemberWithPropertyOfType<EpplusTableColumnAttribute>())
            {
                sort = true;
                var index = 0;
                foreach (var member in members)
                {
                    var hPrefix = default(string);
                    var sortOrderList = CopyList(sortOrderListArg);
                    if (ShouldIgnoreMember(member, isNestedClass))
                    {
                        continue;
                    }
                    if (member.HasPropertyOfType<EpplusIgnore>())
                    {
                        continue;
                    }
                    var memberPath = path != null ? $"{path}.{member.Name}" : member.Name;
                    if (member.HasPropertyOfType<EpplusNestedTableColumnAttribute>())
                    {
                        var memberType = GetTypeByMemberInfo(member);
                        if (memberType == typeof(string) || (!memberType.IsClass && memberType.IsInterface))
                        {
                            throw new InvalidOperationException($"EpplusNestedTableColumn attribute can only be used with complex types (member: {memberPath})");
                        }
                        var nestedTableAttr = member.GetFirstAttributeOfType<EpplusNestedTableColumnAttribute>();
                        var attrOrder = nestedTableAttr.Order;
                        hPrefix = nestedTableAttr.HeaderPrefix;
                        if(!string.IsNullOrEmpty(headerPrefix) && !string.IsNullOrEmpty(hPrefix))
                        {
                            hPrefix = $"{headerPrefix} {hPrefix}";
                        }
                        else if(!string.IsNullOrEmpty(headerPrefix))
                        {
                            hPrefix = headerPrefix;
                        }
                        if(_sortOrderColumns != null && _sortOrderColumns.IndexOf(memberPath) > -1)
                        {
                            attrOrder = _sortOrderColumns.IndexOf(memberPath);
                        }
                        else
                        {
                            attrOrder += SortOrderOffset;
                        }
                        if(sortOrderList == null)
                        {
                            sortOrderList = new List<int>
                            {
                                attrOrder
                            };
                        }
                        else
                        {
                            sortOrderList.Add(attrOrder);
                            if (attrOrder < SortOrderOffset)
                            {
                                sortOrderList[0] = _sortOrderColumns.IndexOf(memberPath);
                            }
                        }
                        SetupInternal(memberType, result, sortOrderList, true, memberPath, hPrefix);
                        sortOrderList.RemoveAt(sortOrderList.Count - 1);
                        continue;
                    }
                    var header = default(string);
                    var sortOrderColumnsIndex = _sortOrderColumns != null ? _sortOrderColumns.IndexOf(memberPath) : -1;
                    var sortOrder = sortOrderColumnsIndex > -1 ? sortOrderColumnsIndex : SortOrderOffset;
                    var hidden = false;
                    var numberFormat = string.Empty;
                    var rowFunction = RowFunctions.None;
                    var totalsRowNumberFormat = string.Empty;
                    var totalsRowLabel = string.Empty;
                    var totalsRowFormula = string.Empty;
                    var colInfoSortOrderList = new List<int>();
                    var epplusColumnAttr = member.GetFirstAttributeOfType<EpplusTableColumnAttribute>();
                    if (epplusColumnAttr != null)
                    {
                        hidden = epplusColumnAttr.Hidden;
                        if(!string.IsNullOrEmpty(epplusColumnAttr.Header) && !string.IsNullOrEmpty(headerPrefix))
                        {
                            header = $"{headerPrefix} {epplusColumnAttr.Header}";
                        }
                        else
                        {
                            header = epplusColumnAttr.Header;
                        }
                        sortOrder = sortOrderColumnsIndex > -1 ? sortOrderColumnsIndex : epplusColumnAttr.Order + SortOrderOffset;
                        
                        if(sortOrderList != null && sortOrderList.Any())
                        {
                            if(sortOrderColumnsIndex > -1)
                            {
                                sortOrderList[0] = sortOrder;
                            }    
                            colInfoSortOrderList.AddRange(sortOrderList);
                        }
                        colInfoSortOrderList.Add(sortOrder < SortOrderOffset ? sortOrder : epplusColumnAttr.Order + SortOrderOffset);
                        numberFormat = epplusColumnAttr.NumberFormat;
                        rowFunction = epplusColumnAttr.TotalsRowFunction;
                        totalsRowNumberFormat = epplusColumnAttr.TotalsRowNumberFormat;
                        totalsRowLabel = epplusColumnAttr.TotalsRowLabel;
                        totalsRowFormula = epplusColumnAttr.TotalsRowFormula;
                    }
                    else if(!string.IsNullOrEmpty(headerPrefix))
                    {
                        header = string.IsNullOrEmpty(header) ? member.Name : header;
                        header = $"{headerPrefix} {header}";
                    }
                    else
                    {
                        header = string.IsNullOrEmpty(header) ? member.Name : header;
                    }
                    result.Add(new ColumnInfo
                    {
                        Header = header,
                        SortOrder = sortOrder,
                        Index = index++,
                        Hidden = hidden,
                        SortOrderLevels = colInfoSortOrderList,
                        MemberInfo = member,
                        NumberFormat = numberFormat,
                        TotalsRowFunction = rowFunction,
                        TotalsRowNumberFormat = totalsRowNumberFormat,
                        TotalsRowLabel = totalsRowLabel,
                        TotalsRowFormula = totalsRowFormula,
                        Path = memberPath
                    });
                }
            }
            else
            {
                var index = 0;
                result.AddRange(members
                    .Where(x => !x.HasPropertyOfType<EpplusIgnore>() && !ShouldIgnoreMember(x, isNestedClass))
                    .Select(member => {
                        var h = default(string);
                        var mp = default(string);
                        if (!string.IsNullOrEmpty(path))
                        {
                            mp = $"{path}.{member.Name}";
                        }
                        var colInfoSortOrderList = new List<int>();
                        var sortOrderColumnsIndex = _sortOrderColumns != null ? _sortOrderColumns.IndexOf(mp) : -1;
                        var sortOrder = sortOrderColumnsIndex > -1 ? sortOrderColumnsIndex : SortOrderOffset;
                        var sortOrderList = CopyList(sortOrderListArg);
                        var epplusColumnAttr = member.GetFirstAttributeOfType<EpplusTableColumnAttribute>();
                        if (epplusColumnAttr != null)
                        {
                            h = epplusColumnAttr.Header;
                            sortOrder = sortOrderColumnsIndex > -1 ? sortOrderColumnsIndex : epplusColumnAttr.Order + SortOrderOffset;
                        }
                        else
                        {
                            h = member.Name;
                        }

                        if (sortOrderList != null && sortOrderList.Any())
                        {
                            if (sortOrderColumnsIndex > -1)
                            {
                                sortOrderList[0] = sortOrder;
                            }
                            colInfoSortOrderList.AddRange(sortOrderList);
                        }
                        
                        if(!string.IsNullOrEmpty(headerPrefix))
                        {
                            h = $"{headerPrefix} {h}";
                        }
                        else
                        {
                            h = member.Name;
                        }
                        return new ColumnInfo { 
                            Index = index++, 
                            MemberInfo = member, 
                            Path = mp, 
                            Header = h,
                            SortOrder = sortOrder,
                            SortOrderLevels = colInfoSortOrderList
                        };
                    }));
            }
            var formulaColumnAttributes = type.FindAttributesOfType<EpplusFormulaTableColumnAttribute>();
            if (formulaColumnAttributes != null && formulaColumnAttributes.Any())
            {
                sort = true;
                foreach (var attr in formulaColumnAttributes)
                {
                    result.Add(new ColumnInfo
                    {
                        SortOrder = attr.Order + SortOrderOffset,
                        Header = attr.Header,
                        Formula = attr.Formula,
                        FormulaR1C1 = attr.FormulaR1C1,
                        NumberFormat = attr.NumberFormat,
                        TotalsRowFunction = attr.TotalsRowFunction,
                        TotalsRowNumberFormat = attr.TotalsRowNumberFormat
                    });
                }
            }
            return sort;
        }

        private Type GetTypeByMemberInfo(MemberInfo member)
        {
            switch(member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                default:
                    throw new InvalidOperationException($"LoadFromCollection: Unsupported MemberType on member {member.Name}. Only Field, Property and Method allowed.");
            }
        }

        private static void ReindexAndSortColumns(List<ColumnInfo> result)
        {
            var index = 0;
            //result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            result.Sort((a, b) =>
            {
                var so1 = a.SortOrderLevels;
                var so2 = b.SortOrderLevels;
                if (so1 == null || so2 == null)
                {
                    if(a.SortOrder == b.SortOrder)
                    {
                        return a.Index.CompareTo(b.Index);
                    }
                    else
                    {
                        return a.SortOrder.CompareTo(b.SortOrder);
                    }
                }
                else if (!so1.Any() || !so2.Any())
                {
                    return a.SortOrder.CompareTo(b.SortOrder);
                }
                else
                {
                    var maxIx = so1.Count < so2.Count ? so1.Count : so2.Count;
                    for(var ix = 0; ix < maxIx; ix++)
                    {
                        var aVal = so1[ix];
                        var bVal = so2[ix];
                        if (aVal.CompareTo(bVal) == 0) continue;
                        return aVal.CompareTo(bVal);
                    }
                    return a.Index.CompareTo(b.Index);
                }
            });
            result.ForEach(x => x.Index = index++);
        }        
    }
}
