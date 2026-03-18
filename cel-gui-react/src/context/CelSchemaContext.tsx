import React, { createContext, useContext, ReactNode } from 'react';
import { CelSchema } from '../types.ts';

const CelSchemaContext = createContext<CelSchema | undefined>(undefined);

export interface CelSchemaProviderProps {
  schema?: CelSchema;
  children: ReactNode;
}

export const CelSchemaProvider: React.FC<CelSchemaProviderProps> = ({
  schema,
  children,
}) => {
  return (
    <CelSchemaContext.Provider value={schema}>
      {children}
    </CelSchemaContext.Provider>
  );
};

export function useCelSchema() {
  return useContext(CelSchemaContext);
}
