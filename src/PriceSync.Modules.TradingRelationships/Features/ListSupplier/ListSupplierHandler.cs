using PriceSync.Modules.OrganizationAccess.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace PriceSync.Modules.TradingRelationships.Features.ListSupplier;

internal class ListSupplierHandler
{
    private readonly ICurrentPerson _currentPerson;
    private readonly ICurrentOrganization _currentOrganization;
    private readonly IOrganizationAuthorizer _organizationAuthorizer;
    private readonly IOrganizationStatusReader _organizationStatusReader;

    public ListSupplierHandler(
        ICurrentPerson currentPerson, 
        ICurrentOrganization currentOrganization, 
        IOrganizationAuthorizer organizationAuthorizer, 
        IOrganizationStatusReader organizationStatusReader)
    {
        _currentPerson = currentPerson;
        _currentOrganization = currentOrganization;
        _organizationAuthorizer = organizationAuthorizer;
        _organizationStatusReader = organizationStatusReader;
    }
    public async Task<ListSupplierHandlerResult> HandleAsync(ListSupplierQuery query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
