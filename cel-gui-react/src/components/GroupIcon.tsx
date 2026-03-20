import React from 'react';

export const GroupIcon: React.FC = () => (
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
    <circle cx="4" cy="4" r="1.6" />
    <circle cx="12" cy="4" r="1.6" />
    <circle cx="8" cy="12" r="1.6" />
    <path d="M4 5.8v1.4c0 .8.5 1.5 1.3 1.8L8 10.1" />
    <path d="M12 5.8v1.4c0 .8-.5 1.5-1.3 1.8L8 10.1" />
  </svg>
);
