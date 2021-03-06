using System;
using System.Diagnostics;
using Api;
using Grpc.Core;

namespace DgraphDotNet {

	internal interface IGRPCConnection : IDisposable {
		Status LastKnownStatus { get; set; }
		void Alter(Api.Operation op);
        Api.Version CheckVersion();
		Response Query(Api.Request req);
		Assigned Mutate(Api.Mutation mut);
		void Commit(TxnContext context);
		void Discard(TxnContext context);
	}

	/// <summary>
	/// A gRPC connection wrapping a <c>Protos.Dgraph.DgraphClient</c>.  
	/// Doesn't check for rpcExceptions or other failures --- it's the job of the calling
	/// classes to know what to do if a connection is faulty.
	/// </summary>
	internal class GRPCConnection : IGRPCConnection {

		private readonly Api.Dgraph.DgraphClient connection;
		private readonly Channel channel;

		// grpc chans also have public ChannelState State { get; }
		// https://grpc.io/grpc/csharp/api/Grpc.Core.ChannelState.html

		public Status LastKnownStatus { get; set; }

		/// <remarks>
		///       Pre : <c>channel != null</c> <c>connection != null</c> and 
		///       this is the channel used to make the connection. 
		/// </remarks>
		public GRPCConnection(Channel channel, Api.Dgraph.DgraphClient connection) {
			Debug.Assert(channel != null);
			Debug.Assert(connection != null);

			this.channel = channel;
			this.connection = connection;

			LastKnownStatus = Status.DefaultSuccess;
		}

		public bool ConnectionOK => LastKnownStatus.StatusCode == StatusCode.OK;

		// FIXME: add checkversion

		// FIXME: should allow cancellation tokens, deadlines, etc??

		#region mutations

		public void Alter(Api.Operation op) {
			AssertNotDisposed();

			connection.Alter(op);
		}

		public Api.Version CheckVersion() {
			AssertNotDisposed();

			return connection.CheckVersion(new Check());
		}

		public Response Query(Api.Request req) {
			AssertNotDisposed();

			return connection.Query(req);
		}

		public Assigned Mutate(Api.Mutation mut) {
			AssertNotDisposed();

			return connection.Mutate(mut);
		}

		public void Commit(TxnContext context) {
			AssertNotDisposed();

			connection.CommitOrAbort(context);
		}

		public void Discard(TxnContext context) {
			AssertNotDisposed();

			connection.CommitOrAbort(context);
		}

		#endregion

		// 
		// ------------------------------------------------------
		//              disposable pattern.
		// ------------------------------------------------------
		//
		#region disposable pattern

		// From the docs : https://grpc.io/grpc/csharp/api/Grpc.Core.Channel.html
		//
		// "It is strongly recommended to shutdown all previously created channels before exiting from the process."
		//
		// I could treat this like an unmanaged resource, but that would mean using a finalizer and should I really
		// be calling async code in there, setting up new tasks and doing network ops? And what happends when all 
		// that's called during program exit?  
		// Seems best to treat as a managed resource and just expect the user to call Dispose(). 

		private bool disposed; // = false;

		protected void AssertNotDisposed() {
			if (this.disposed) {
				throw new ObjectDisposedException(GetType().Name);
			}
		}

		public void Dispose() {
			if (!this.disposed) {
				// returns a Task, but ignoring
				channel.ShutdownAsync(); // exceptions ???
			}
			this.disposed = true;
		}

		#endregion
	}

}