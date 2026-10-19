import { useState } from 'react';
import { Coffee } from 'lucide-react';

export function ProductPhoto({ src, alt, className = '', eager = false }: { src?: string | null; alt: string; className?: string; eager?: boolean }) {
  const [failed, setFailed] = useState<string | null>(null);
  return <div className={`product-photo ${className}`}>
    {src && failed !== src ? <img src={src} alt={alt} loading={eager ? 'eager' : 'lazy'} decoding="async" width="960" height="960" onError={() => setFailed(src)} /> : <div className="photo-fallback" aria-label={alt}><Coffee size={38} strokeWidth={1.2} /></div>}
  </div>;
}
