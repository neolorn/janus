using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// Builds the host's composition for one step-up scenario of the truth table, with the
/// assurance provider it is given registered in the deployment's place.
/// </summary>
/// <param name="assurance">
/// What the composition registers as <see cref="IAssuranceProvider"/>, or nothing for a
/// composition that registers none.
/// </param>
/// <param name="cancellationToken">Abandons the build.</param>
/// <returns>
/// The composition, over the deployment's own database and started as the host starts
/// it. The suite disposes it once the scenario is judged, where it can be disposed.
/// </returns>
/// <remarks>
/// Implements LIB-TEST-001 AC2 and LIB-HOST-004 (D-188, D-189). The suite calls it once
/// for each step-up case, with a provider of its own that gives the scenario's report
/// or fails to give one, and with none for the scenario of a deployment that has no
/// provider. Whatever provider the host's own composition registers is left out of the
/// one built here. A host whose table holds no step-up case gives the suite none.
/// </remarks>
public delegate ValueTask<IServiceProvider> DeploymentFactory(
    IAssuranceProvider? assurance,
    CancellationToken cancellationToken);
