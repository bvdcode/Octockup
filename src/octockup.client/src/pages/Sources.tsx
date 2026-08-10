import { ModuleCatalogPage } from "../components/modules/ModuleCatalogPage";
import { ModuleDestination } from "../types/api";

export function SourcesPage() {
  return (
    <ModuleCatalogPage
      destination={ModuleDestination.Source}
      providerType="source"
      route="/sources"
      translationPrefix="sources"
      emptyKey="noSources"
    />
  );
}

export default SourcesPage;
