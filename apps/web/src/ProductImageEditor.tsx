import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from './api';
import { ProductPhoto } from './ProductPhoto';
import type { AdminProduct } from './adminTypes';

type SavedImage = { imageUrl: string | null; imageAlt: string | null; version: number };
export function ProductImageEditor({ product, saved }: { product: AdminProduct; saved: (image: SavedImage) => void }) {
  const qc = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [alt, setAlt] = useState(product.imageAlt || product.name);
  const [validation, setValidation] = useState('');
  const [notice, setNotice] = useState('');
  const change = useMutation({
    mutationFn: (remove: boolean) => api<SavedImage>(`/api/admin/products/${product.id}/image?expectedVersion=${product.version}&alt=${encodeURIComponent(alt)}`, { method: remove ? 'DELETE' : 'PUT', ...(remove ? {} : { headers: { 'Content-Type': file?.type || 'application/octet-stream' }, body: file }) }),
    onSuccess: image => { saved(image); setFile(null); setNotice('Görsel kaydedildi. Diğer ürün değişikliklerini aşağıdaki Kaydet ile tamamlayın.'); void qc.invalidateQueries({ queryKey: ['admin'] }); void qc.invalidateQueries({ queryKey: ['menu'] }); }
  });
  if (!product.id) return <p className="small-note">Görsel eklemek için önce ürünü kaydedin, ardından düzenleyin.</p>;
  return <section className="image-editor" aria-label="Ürün görseli">
    <strong>Ürün görseli</strong>
    <ProductPhoto src={product.imageUrl} alt={product.imageAlt || product.name} />
    <label>Yeni görsel<input key={product.version} type="file" accept="image/jpeg,image/png,image/webp" disabled={change.isPending} onChange={e => { const next = e.target.files?.[0] || null; setNotice(''); setValidation(next && next.size > 5 * 1024 * 1024 ? 'Görsel en fazla 5 MB olabilir.' : ''); setFile(next && next.size <= 5 * 1024 * 1024 ? next : null); }} /></label>
    <label>Yeni görsel açıklaması<input maxLength={200} value={alt} onChange={e => setAlt(e.target.value)} placeholder={product.name} /></label>
    <p className="small-note">JPG, PNG veya WebP · En fazla 5 MB. Görsel değişikliği hemen kaydedilir.</p>
    <div className="image-editor-actions"><button type="button" className="secondary" disabled={!file || change.isPending} onClick={() => change.mutate(false)}>{change.isPending ? 'Kaydediliyor…' : 'Görseli yükle'}</button>{product.imageUrl && <button type="button" className="secondary" disabled={change.isPending} onClick={() => change.mutate(true)}>Görseli kaldır</button>}</div>
    {(validation || change.error) && <p className="error" role="alert">{validation || change.error?.message}</p>}
    {notice && <p className="notice" role="status">{notice}</p>}
  </section>;
}
