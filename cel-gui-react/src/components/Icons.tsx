import React from 'react';

export const InfoIcon: React.FC = () => (
  <svg
    aria-hidden="true"
    viewBox="0 0 16 16"
    width="14"
    height="14"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
    strokeLinecap="round"
    strokeLinejoin="round"
  >
    <circle cx="8" cy="8" r="7" />
    <line x1="8" y1="11" x2="8" y2="8" />
    <line x1="8" y1="5" x2="8" y2="5" />
  </svg>
);

export const ChevronDownIcon: React.FC = () => (
  <svg
    aria-hidden="true"
    viewBox="0 0 16 16"
    width="14"
    height="14"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
    strokeLinecap="round"
    strokeLinejoin="round"
  >
    <path d="M4 6l4 4 4-4" />
  </svg>
);

export const ChevronRightIcon: React.FC = () => (
  <svg
    aria-hidden="true"
    viewBox="0 0 16 16"
    width="14"
    height="14"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
    strokeLinecap="round"
    strokeLinejoin="round"
  >
    <path d="M6 4l4 4-4 4" />
  </svg>
);

export const WarningIcon: React.FC = () => (
  <svg
    aria-hidden="true"
    viewBox="0 0 16 16"
    width="14"
    height="14"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
  >
    <path d="M8 2l7 12H1L8 2z" />
    <line x1="8" y1="8" x2="8" y2="10" />
    <line x1="8" y1="12" x2="8" y2="12" />
  </svg>
);
