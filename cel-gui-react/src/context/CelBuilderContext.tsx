import React, { createContext, useContext, useMemo, ReactNode } from 'react';

export interface CelBuilderContextValue {
  readOnly?: boolean;
}

const CelBuilderContext = createContext<CelBuilderContextValue>({
  readOnly: false,
});

export interface CelBuilderProviderProps {
  readOnly?: boolean;
  children: ReactNode;
}

export const CelBuilderProvider: React.FC<CelBuilderProviderProps> = ({
  readOnly,
  children,
}) => {
  const value = useMemo(() => ({ readOnly }), [readOnly]);
  return (
    <CelBuilderContext.Provider value={value}>
      {children}
    </CelBuilderContext.Provider>
  );
};

export function useCelBuilder() {
  return useContext(CelBuilderContext);
}
