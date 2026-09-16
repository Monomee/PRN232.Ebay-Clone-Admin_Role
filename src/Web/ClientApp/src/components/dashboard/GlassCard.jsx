import React from 'react';

export const GlassCard = ({ children, className = "", stagger = "" }) => (
  <div className={`glass-panel rounded-4 ${className} animate-fade-in-up ${stagger}`}>
    {children}
  </div>
);
