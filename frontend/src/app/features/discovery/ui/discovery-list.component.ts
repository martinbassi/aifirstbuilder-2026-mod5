import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NearbyMuralItemResponse } from '../../../core/api-client/api-client.generated';

/**
 * List side of the `discovery` feature (spec Block 7). Renders `items` in the exact order
 * received — the backend already orders by `distanceKm` (spec Block 2, AC-05), so this component
 * never re-sorts, per spec. Selecting an item shows its detail inline (photo/date/location,
 * AC-04) using ONLY the fields already present on `NearbyMuralItemResponse` — no extra endpoint
 * call, as documented in the spec ("sin golpear ningún endpoint adicional"). Also emits
 * `muralSelected` so a parent (`discovery-page`) can sync the selection with the map.
 */
@Component({
  selector: 'app-discovery-list',
  standalone: true,
  imports: [NzCardModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './discovery-list.component.html',
})
export class DiscoveryListComponent {
  readonly items = input<NearbyMuralItemResponse[]>([]);
  readonly muralSelected = output<NearbyMuralItemResponse>();

  readonly selectedItem = signal<NearbyMuralItemResponse | null>(null);

  select(item: NearbyMuralItemResponse): void {
    this.selectedItem.set(item);
    this.muralSelected.emit(item);
  }
}
