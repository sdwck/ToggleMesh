import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface OrganizationState {
    activeOrganizationId: string | null;
    setActiveOrganizationId: (id: string | null) => void;
}

export const useOrganizationStore = create<OrganizationState>()(
    persist(
        (set) => ({
            activeOrganizationId: null,
            setActiveOrganizationId: (id) => set({ activeOrganizationId: id }),
        }),
        {
            name: 'togglemesh-org-storage',
        }
    )
);
