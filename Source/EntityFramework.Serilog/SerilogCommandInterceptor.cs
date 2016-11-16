// Based on System.Data.Entity.Infrastructure.Interception.DatabaseLogFormatter

using System;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Interception;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Serilog;

namespace EntityFramework.Serilog
{
    class SerilogCommandInterceptor : IDbCommandInterceptor, IDbConnectionInterceptor, IDbTransactionInterceptor, IDisposable
    {
        private readonly ILogger _Logger;
        private readonly WeakReference _Context;
        private readonly Stopwatch _Stopwatch = new Stopwatch();

        /// <summary>
        /// Creates a formatter that will not filter by any <see cref="DbContext" /> and will instead log every command
        /// from any context and also commands that do not originate from a context.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public SerilogCommandInterceptor(ILogger logger = null)
        {
            _Logger = logger ?? Log.Logger;
        }

        /// <summary>
        /// Creates a formatter that will only log commands the come from the given <see cref="DbContext" /> instance.
        /// </summary>
        /// <param name="context">The context for which commands should be logged. Pass null to log every command
        /// from any context and also commands that do not originate from a context.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="System.ArgumentNullException">context</exception>
        public SerilogCommandInterceptor(DbContext context, ILogger logger)
            : this(logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            _Context = new WeakReference(context);
            RemoveInterceptorWhenContextDisposing(context);
        }

        /// <summary>
        /// Removes the interceptor when the context is disposing.
        /// </summary>
        /// <param name="context">The context.</param>
        private void RemoveInterceptorWhenContextDisposing(DbContext context)
        {
            object internalContext = typeof(DbContext).GetProperty("InternalContext", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true).Invoke(context, null);
            EventInfo eventInfo = internalContext.GetType().GetEvent("OnDisposing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            eventInfo.AddEventHandler(internalContext, new EventHandler<EventArgs>((_, __) => DbInterception.Remove(this)));
        }

        /// <summary>
        /// The context for which commands are being logged, or null if commands from all contexts are
        /// being logged.
        /// </summary>
        protected virtual DbContext Context
        {
            get
            {
                return _Context != null && _Context.IsAlive ? (DbContext)_Context.Target : null;
            }
        }

        /// <summary>
        /// The stop watch used to time executions. This stop watch is started at the end of
        /// <see cref="NonQueryExecuting" />, <see cref="ScalarExecuting" />, and <see cref="ReaderExecuting" />
        /// methods and is stopped at the beginning of the <see cref="NonQueryExecuted" />, <see cref="ScalarExecuted" />,
        /// and <see cref="ReaderExecuted" /> methods. If these methods are overridden and the stop watch is being used
        /// then the overrides should either call the base method or start/stop the watch themselves.
        /// </summary>
        protected virtual Stopwatch Stopwatch
        {
            get
            {
                return _Stopwatch;
            }
        }

        /// <summary>
        /// Write a log event with the Serilog.Events.LogEventLevel.Information level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        protected virtual void LogInformation(string messageTemplate, params object[] propertyValues)
        {
            _Logger.Information(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Write a log event with the Serilog.Events.LogEventLevel.Error level and associated exception.
        /// </summary>
        /// <param name="exception">Exception related to the event.</param>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        protected virtual void LogError(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            _Logger.Error(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Returns weather we should log.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="interceptionContext">The interception context.</param>
        /// <returns></returns>
        protected virtual bool ShouldLog(DbInterceptionContext interceptionContext)
        {
            return Context == null || interceptionContext.DbContexts.FirstOrDefault(ctx => ReferenceEquals(ctx, Context)) != null;
        }

        /// <summary>
        /// This method is called before a call to <see cref="DbCommand.ExecuteNonQuery" /> or
        /// one of its async counterparts is made.
        /// The default implementation calls <see cref="Executing" /> and starts <see cref="Stopwatch"/>.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            Executing(command, interceptionContext);
            Stopwatch.Restart();
        }

        /// <summary>
        /// This method is called after a call to <see cref="DbCommand.ExecuteNonQuery" /> or
        /// one of its async counterparts is made.
        /// The default implementation stops <see cref="Stopwatch"/> and calls <see cref="Executed" />.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            Stopwatch.Stop();
            Executed(command, interceptionContext);
        }

        /// <summary>
        /// This method is called before a call to <see cref="DbCommand.ExecuteReader(CommandBehavior)" /> or
        /// one of its async counterparts is made.
        /// The default implementation calls <see cref="Executing" /> and starts <see cref="Stopwatch"/>.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            Executing(command, interceptionContext);
            Stopwatch.Restart();
        }

        /// <summary>
        /// This method is called after a call to <see cref="DbCommand.ExecuteReader(CommandBehavior)" /> or
        /// one of its async counterparts is made.
        /// The default implementation stops <see cref="Stopwatch"/> and calls <see cref="Executed" />.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            Stopwatch.Stop();
            Executed(command, interceptionContext);
        }

        /// <summary>
        /// This method is called before a call to <see cref="DbCommand.ExecuteScalar" />  or
        /// one of its async counterparts is made.
        /// The default implementation calls <see cref="Executing" /> and starts <see cref="Stopwatch"/>.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            Executing(command, interceptionContext);
            Stopwatch.Restart();
        }

        /// <summary>
        /// This method is called after a call to <see cref="DbCommand.ExecuteScalar" />  or
        /// one of its async counterparts is made.
        /// The default implementation stops <see cref="Stopwatch"/> and calls <see cref="Executed" />.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            Stopwatch.Stop();
            Executed(command, interceptionContext);
        }

        /// <summary>
        /// Called whenever a command is about to be executed. The default implementation of this method
        /// filters by <see cref="DbContext" /> set into <see cref="Context" />, if any, and then calls
        /// <see cref="LogCommand" />. This method would typically only be overridden to change the
        /// context filtering behavior.
        /// </summary>
        /// <typeparam name="TResult">The type of the operation's results.</typeparam>
        /// <param name="command">The command that will be executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the command.</param>
        public virtual void Executing<TResult>(DbCommand command, DbCommandInterceptionContext<TResult> interceptionContext)
        {
            if (ShouldLog(interceptionContext))
            {
                LogCommand(command, interceptionContext);
            }
        }

        /// <summary>
        /// Called whenever a command has completed executing. The default implementation of this method
        /// filters by <see cref="DbContext" /> set into <see cref="Context" />, if any, and then calls
        /// <see cref="LogResult" />. This method would typically only be overridden to change the context
        /// filtering behavior.
        /// </summary>
        /// <typeparam name="TResult">The type of the operation's results.</typeparam>
        /// <param name="command">The command that was executed.</param>
        /// <param name="interceptionContext">Contextual information associated with the command.</param>
        public virtual void Executed<TResult>(DbCommand command, DbCommandInterceptionContext<TResult> interceptionContext)
        {
            if (ShouldLog(interceptionContext))
            {
                LogResult(command, interceptionContext);
            }
        }

        /// <summary>
        /// Called to log a command that is about to be executed. Override this method to change how the
        /// command is logged to <see cref="WriteAction" />.
        /// </summary>
        /// <typeparam name="TResult">The type of the operation's results.</typeparam>
        /// <param name="command">The command to be logged.</param>
        /// <param name="interceptionContext">Contextual information associated with the command.</param>
        public virtual void LogCommand<TResult>(DbCommand command, DbCommandInterceptionContext<TResult> interceptionContext)
        {
            var commandText = command.CommandText ?? "<null>";

            LogInformation(commandText);

            if (command.Parameters != null)
            {
                foreach (var parameter in command.Parameters.OfType<DbParameter>())
                {
                    LogParameter(command, interceptionContext, parameter);
                }
            }

            LogInformation(interceptionContext.IsAsync ? "-- Executing asynchronously at {now}" : "-- Executing at {DateTime}", DateTimeOffset.Now);
        }

        /// <summary>
        /// Called by <see cref="LogCommand" /> to log each parameter. This method can be called from an overridden
        /// implementation of <see cref="LogCommand" /> to log parameters, and/or can be overridden to
        /// change the way that parameters are logged to <see cref="WriteAction" />.
        /// </summary>
        /// <typeparam name="TResult">The type of the operation's results.</typeparam>
        /// <param name="command">The command being logged.</param>
        /// <param name="interceptionContext">Contextual information associated with the command.</param>
        /// <param name="parameter">The parameter to log.</param>
        public virtual void LogParameter<TResult>(DbCommand command, DbCommandInterceptionContext<TResult> interceptionContext, DbParameter parameter)
        {
            // -- Name: [Value] (Type = {}, Direction = {}, IsNullable = {}, Size = {}, Precision = {} Scale = {})
            var builder = new StringBuilder();
            builder.Append("-- ")
                .Append(parameter.ParameterName)
                .Append(": '")
                .Append((parameter.Value == null || parameter.Value == DBNull.Value) ? "null" : parameter.Value)
                .Append("' (Type = ")
                .Append(parameter.DbType);

            if (parameter.Direction != ParameterDirection.Input)
            {
                builder.Append(", Direction = ").Append(parameter.Direction);
            }

            if (!parameter.IsNullable)
            {
                builder.Append(", IsNullable = false");
            }

            if (parameter.Size != 0)
            {
                builder.Append(", Size = ").Append(parameter.Size);
            }

            if (((IDbDataParameter)parameter).Precision != 0)
            {
                builder.Append(", Precision = ").Append(((IDbDataParameter)parameter).Precision);
            }

            if (((IDbDataParameter)parameter).Scale != 0)
            {
                builder.Append(", Scale = ").Append(((IDbDataParameter)parameter).Scale);
            }

            builder.Append(")").Append(Environment.NewLine);

            LogInformation(builder.ToString());
        }

        /// <summary>
        /// Called to log the result of executing a command. Override this method to change how results are
        /// logged to <see cref="WriteAction" />.
        /// </summary>
        /// <typeparam name="TResult">The type of the operation's results.</typeparam>
        /// <param name="command">The command being logged.</param>
        /// <param name="interceptionContext">Contextual information associated with the command.</param>
        public virtual void LogResult<TResult>(DbCommand command, DbCommandInterceptionContext<TResult> interceptionContext)
        {
            if (interceptionContext.Exception != null)
            {
                LogError(interceptionContext.Exception, "-- Failed in {duration} ms with error: {error}", Stopwatch.ElapsedMilliseconds, interceptionContext.Exception.Message);
            }
            else if (interceptionContext.TaskStatus.HasFlag(TaskStatus.Canceled))
            {
                LogInformation("-- Canceled in {duration} ms", Stopwatch.ElapsedMilliseconds);
            }
            else
            {
                var result = interceptionContext.Result;
                var resultString = (object)result == null
                    ? "<null>"
                    : (result is DbDataReader)
                        ? result.GetType().Name
                        : result.ToString();
                LogInformation("-- Completed in {duration} ms with result: {result}", Stopwatch.ElapsedMilliseconds, resultString);
            }
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection beginning the transaction.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void BeginningTransaction(DbConnection connection, BeginTransactionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// Called after <see cref="DbConnection.BeginTransaction(Data.IsolationLevel)" /> is invoked.
        /// The default implementation of this method filters by <see cref="DbContext" /> set into
        /// <see cref="Context" />, if any, and then logs the event.
        /// </summary>
        /// <param name="connection">The connection that began the transaction.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void BeganTransaction(DbConnection connection, BeginTransactionInterceptionContext interceptionContext)
        {
            if (ShouldLog(interceptionContext))
            {
                if (interceptionContext.Exception != null)
                {
                    LogError(interceptionContext.Exception, "Failed to start transaction at {now} with error: {error}", DateTimeOffset.Now, interceptionContext.Exception.Message);
                }
                else
                {
                    LogInformation("Started transaction at {now},", DateTimeOffset.Now);
                }
            }
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void EnlistingTransaction(DbConnection connection, EnlistTransactionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void EnlistedTransaction(DbConnection connection, EnlistTransactionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection being opened.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Opening(DbConnection connection, DbConnectionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// Called after <see cref="DbConnection.Open" /> or its async counterpart is invoked.
        /// The default implementation of this method filters by <see cref="DbContext" /> set into
        /// <see cref="Context" />, if any, and then logs the event.
        /// </summary>
        /// <param name="connection">The connection that was opened.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Opened(DbConnection connection, DbConnectionInterceptionContext interceptionContext)
        {
            if (ShouldLog(interceptionContext))
            {
                if (interceptionContext.Exception != null)
                {
                    LogError(interceptionContext.Exception, interceptionContext.IsAsync ?
                                                                "Failed to open connection asynchronously at {now} with error: {error}" :
                                                                "Failed to open connection at {now} with error: {error}", DateTimeOffset.Now, interceptionContext.Exception.Message);
                }
                else if (interceptionContext.TaskStatus.HasFlag(TaskStatus.Canceled))
                {
                    LogInformation("Canceled open connection at {now}", DateTimeOffset.Now);
                }
                else
                {
                    LogInformation(interceptionContext.IsAsync ? "Opened connection asynchronously at {now}" : "Opened connection at {now}", DateTimeOffset.Now);
                }
            }
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection being closed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Closing(DbConnection connection, DbConnectionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// Called after <see cref="DbConnection.Close" /> is invoked.
        /// The default implementation of this method filters by <see cref="DbContext" /> set into
        /// <see cref="Context" />, if any, and then logs the event.
        /// </summary>
        /// <param name="connection">The connection that was closed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Closed(DbConnection connection, DbConnectionInterceptionContext interceptionContext)
        {
            if (ShouldLog(interceptionContext))
            {
                if (interceptionContext.Exception != null)
                {
                    LogError(interceptionContext.Exception, "Failed to close connection at {now} with error: {error}", DateTimeOffset.Now, interceptionContext.Exception.Message);
                }
                else
                {
                    LogInformation("Closed connection at {now}", DateTimeOffset.Now);
                }
            }
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionStringGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionStringGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionStringSetting(
            DbConnection connection, DbConnectionPropertyInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionStringSet(
            DbConnection connection, DbConnectionPropertyInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionTimeoutGetting(DbConnection connection, DbConnectionInterceptionContext<int> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionTimeoutGot(DbConnection connection, DbConnectionInterceptionContext<int> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void DatabaseGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void DatabaseGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void DataSourceGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void DataSourceGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Called before <see cref="Component.Dispose()" /> is invoked.
        /// The default implementation of this method filters by <see cref="DbContext" /> set into
        /// <see cref="Context" />, if any, and then logs the event.
        /// </summary>
        /// <param name="connection">The connection being disposed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Disposing(DbConnection connection, DbConnectionInterceptionContext interceptionContext)
        {
            if (ShouldLog(interceptionContext) && connection.State == ConnectionState.Open)
            {
                LogInformation("Disposed connection at {now}", DateTimeOffset.Now);
            }
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection that was disposed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Disposed(DbConnection connection, DbConnectionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ServerVersionGetting(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ServerVersionGot(DbConnection connection, DbConnectionInterceptionContext<string> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void StateGetting(DbConnection connection, DbConnectionInterceptionContext<ConnectionState> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void StateGot(DbConnection connection, DbConnectionInterceptionContext<ConnectionState> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionGetting(DbTransaction transaction, DbTransactionInterceptionContext<DbConnection> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void ConnectionGot(DbTransaction transaction, DbTransactionInterceptionContext<DbConnection> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden. </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void IsolationLevelGetting(DbTransaction transaction, DbTransactionInterceptionContext<IsolationLevel> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void IsolationLevelGot(DbTransaction transaction, DbTransactionInterceptionContext<IsolationLevel> interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="transaction">The transaction being commited.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Committing(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// This method is called after <see cref="DbTransaction.Commit" /> is invoked.
        /// The default implementation of this method filters by <see cref="DbContext" /> set into
        /// <see cref="Context" />, if any, and then logs the event.
        /// </summary>
        /// <param name="transaction">The transaction that was commited.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Committed(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
            if (ShouldLog(interceptionContext))
            {
                if (interceptionContext.Exception != null)
                {
                    LogError(interceptionContext.Exception, "Failed to commit transaction at {now} with error: {error}", DateTimeOffset.Now, interceptionContext.Exception.Message);
                }
                else
                {
                    LogInformation("Committed transaction at {now}", DateTimeOffset.Now);
                }
            }
        }

        /// <summary>
        /// This method is called before <see cref="DbTransaction.Dispose()" /> is invoked.
        /// The default implementation of this method filters by <see cref="DbContext" /> set into
        /// <see cref="Context" />, if any, and then logs the event.
        /// </summary>
        /// <param name="transaction">The transaction being disposed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Disposing(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
            if (ShouldLog(interceptionContext) && transaction.Connection != null)
            {
                LogInformation("Disposed transaction at {now}", DateTimeOffset.Now);
            }
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="transaction">The transaction that was disposed.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void Disposed(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// Does not write to log unless overridden.
        /// </summary>
        /// <param name="transaction">The transaction being rolled back.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void RollingBack(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
        }

        /// <summary>
        /// This method is called after <see cref="DbTransaction.Rollback" /> is invoked.
        /// The default implementation of this method filters by <see cref="DbContext" /> set into
        /// <see cref="Context" />, if any, and then logs the event.
        /// </summary>
        /// <param name="transaction">The transaction that was rolled back.</param>
        /// <param name="interceptionContext">Contextual information associated with the call.</param>
        public virtual void RolledBack(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
            if (ShouldLog(interceptionContext))
            {
                if (interceptionContext.Exception != null)
                {
                    LogError(interceptionContext.Exception, "Failed to rollback transaction at {now} with error: {error}", DateTimeOffset.Now, interceptionContext.Exception.Message);
                }
                else
                {
                    LogInformation("Rolled back transaction at {now}", DateTimeOffset.Now);
                }
            }
        }
                
        public void Dispose()
        {
            DbInterception.Remove(this);
            GC.SuppressFinalize(this);
        }
    }
}
