﻿namespace EFCache
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data.Entity.Core.Common.CommandTrees;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Diagnostics;
    using System.Linq;

    internal class CommandTreeFacts
    {
        private static HashSet<string> NonDeterministicFunctions = new HashSet<string>(
            new[] {
                "Edm.CurrentDateTime",
                "Edm.CurrentUtcDateTime",
                "Edm.CurrentDateTimeOffsets",
                "Edm.NewGuid",

                "SqlServer.NEWID",
                "SqlServer.GETDATE",
                "SqlServer.GETUTCDATE",
                "SqlServer.SYSDATETIME",
                "SqlServer.SYSUTCDATETIME",
                "SqlServer.SYSDATETIMEOFFSET",
                "SqlServer.CURRENT_USER",
                "SqlServer.CURRENT_TIMESTAMP",
                "SqlServer.HOST_NAME",
                "SqlServer.USER_NAME",
            },
            StringComparer.OrdinalIgnoreCase);

        public CommandTreeFacts(DbCommandTree commandTree)
        {
            IsQuery = commandTree is DbQueryCommandTree;

            var visitor = new CommandTreeVisitor();

            if (commandTree.CommandTreeKind == DbCommandTreeKind.Query)
            {
                ((DbQueryCommandTree)commandTree).Query.Accept(visitor);
            }
            else
            {
                Debug.Assert(commandTree is DbModificationCommandTree, "Unexpected command tree kind");

                //TODO: need to visit params, return values etc.?
                ((DbModificationCommandTree)commandTree).Target.Expression.Accept(visitor);
            }

            AffectedEntitySets = new ReadOnlyCollection<EntitySetBase>(visitor.EntitySets);
            UsesNonDeterministicFunctions =
                visitor.Functions.Any(f => NonDeterministicFunctions.Contains(
                        string.Format("{0}.{1}", f.NamespaceName, f.Name)));
        }

        internal CommandTreeFacts(ReadOnlyCollection<EntitySetBase> affectedEntitySets, bool isQuery, bool usesNonDeterministicFunctions)
        {
            AffectedEntitySets = affectedEntitySets;
            IsQuery = isQuery;
            UsesNonDeterministicFunctions = usesNonDeterministicFunctions;
        }

        public ReadOnlyCollection<EntitySetBase> AffectedEntitySets
        {
            get;
            private set;
        }

        public bool IsQuery
        {
            get;
            private set;
        }

        public bool UsesNonDeterministicFunctions
        {
            get;
            private set;
        }

        private class CommandTreeVisitor : BasicCommandTreeVisitor
        {
            public readonly List<EntitySetBase> EntitySets = new List<EntitySetBase>();
            public readonly List<EdmFunction> Functions = new List<EdmFunction>();

            public override void Visit(DbScanExpression expression)
            {
                EntitySets.Add(expression.Target);

                base.Visit(expression);
            }

            public override void Visit(DbFunctionExpression expression)
            {
                Functions.Add(expression.Function);

                base.Visit(expression);
            }
        }
    }
}