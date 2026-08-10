import { ModuleCatalogPage } from "../components/modules/ModuleCatalogPage";
import { ModuleDestination } from "../types/api";

export function StoragesPage() {
  return (
    <ModuleCatalogPage
      destination={ModuleDestination.Target}
      providerType="storage"
      route="/storages"
      translationPrefix="storages"
      emptyKey="noStorages"
    />
  );
}

export default StoragesPage;
