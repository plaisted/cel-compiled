import React, { createContext, useContext, useMemo, ReactNode } from 'react';

export interface CelBuilderContextValue {
  readOnly?: boolean;
  layout?: 'standard' | 'natural';
}

const CelBuilderContext = createContext<CelBuilderContextValue>({
  readOnly: false,
  layout: 'standard',
});

export interface CelBuilderProviderProps {
  readOnly?: boolean;
  layout?: 'standard' | 'natural';
  children: ReactNode;
}

export const CelBuilderProvider: React.FC<CelBuilderProviderProps> = ({
  readOnly,
  layout,
  children,
}) => {
  const value = useMemo(() => ({ readOnly, layout }), [readOnly, layout]);
  return (
    <CelBuilderContext.Provider value={value}>
      {children}
    </CelBuilderContext.Provider>
  );
};

export function useCelBuilder() {
  return useContext(CelBuilderContext);
}
